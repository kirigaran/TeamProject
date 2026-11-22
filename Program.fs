namespace TeamProject

open System
open System.IO
open System.Linq
open System.Security.Claims
open System.Security.Cryptography
open System.Threading.Tasks
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

[<AllowNullLiteral>]
type User() =
    member val Id = 0 with get, set
    member val Username = "" with get, set
    member val PasswordHash = "" with get, set
    member val Role = "" with get, set
    member val Project = "" with get, set
    member val GradePoints = 0 with get, set
    member val CreatedAt = DateTime.UtcNow with get, set

[<AllowNullLiteral>]
type WorkTask() =
    member val Id = 0 with get, set
    member val Project = "" with get, set
    member val Title = "" with get, set
    member val Description = "" with get, set
    member val AssignedToId = 0 with get, set
    member val AssignedById = 0 with get, set
    member val CreatedAt = DateTime.UtcNow with get, set
    member val Deadline = DateTime.UtcNow with get, set
    member val Status = "Open" with get, set
    member val ExtensionRequested = false with get, set
    member val Points = 10 with get, set
    member val CompletedAt: Nullable<DateTime> = Nullable() with get, set

[<AllowNullLiteral>]
type ChatMessage() =
    member val Id = 0 with get, set
    member val Project = "" with get, set
    member val SenderId = 0 with get, set
    member val ReceiverId: Nullable<int> = Nullable() with get, set
    member val Body = "" with get, set
    member val AudioUrl = "" with get, set
    member val CreatedAt = DateTime.UtcNow with get, set

type AppDbContext(options: DbContextOptions<AppDbContext>) =
    inherit DbContext(options)
    member this.Users = this.Set<User>()
    member this.Tasks = this.Set<WorkTask>()
    member this.Messages = this.Set<ChatMessage>()

[<CLIMutable>]
type SignupRequest = { username: string; password: string; confirmPassword: string; role: string; project: string }

[<CLIMutable>]
type LoginRequest = { username: string; password: string; role: string; project: string }

[<CLIMutable>]
type CreateTaskRequest = { title: string; description: string; assignedToId: int; hours: int }

[<CLIMutable>]
type ExtensionRequest = { hours: int }

[<CLIMutable>]
type ChatRequest = { body: string; receiverId: Nullable<int> }

module Security =
    let hashPassword (password: string) =
        let salt = RandomNumberGenerator.GetBytes(16)
        let hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32)
        Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash)

    let verifyPassword (password: string) (stored: string) =
        try
            let parts = stored.Split('.')
            if parts.Length <> 2 then false
            else
                let salt = Convert.FromBase64String(parts[0])
                let expected = Convert.FromBase64String(parts[1])
                let actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, expected.Length)
                CryptographicOperations.FixedTimeEquals(actual, expected)
        with _ -> false

module Program =
    let private userId (ctx: HttpContext) =
        Int32.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier))

    let private role (ctx: HttpContext) =
        ctx.User.FindFirstValue(ClaimTypes.Role) |> Option.ofObj |> Option.defaultValue ""

    let private project (ctx: HttpContext) =
        ctx.User.FindFirstValue("project") |> Option.ofObj |> Option.defaultValue ""

    let private signIn (ctx: HttpContext) (user: User) =
        task {
            let claims =
                [ Claim(ClaimTypes.NameIdentifier, string user.Id)
                  Claim(ClaimTypes.Name, user.Username)
                  Claim(ClaimTypes.Role, user.Role)
                  Claim("project", user.Project) ]
            let identity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            let principal = ClaimsPrincipal(identity)
            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)
        }

    
    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)

        let connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")

        if String.IsNullOrWhiteSpace(connectionString) then
            failwith "DATABASE_URL environment variable is missing."

        builder.Services.AddDbContext<AppDbContext>(fun options ->
            options.UseNpgsql(connectionString) |> ignore) |> ignore

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(fun options ->
                options.Cookie.HttpOnly <- true
                options.Cookie.SameSite <- SameSiteMode.Lax
                options.Cookie.SecurePolicy <- CookieSecurePolicy.SameAsRequest
                options.ExpireTimeSpan <- TimeSpan.FromDays(14.0)
                options.SlidingExpiration <- true
                options.Events.OnRedirectToLogin <- fun context ->
                    context.Response.StatusCode <- StatusCodes.Status401Unauthorized
                    Task.CompletedTask
                options.Events.OnRedirectToAccessDenied <- fun context ->
                    context.Response.StatusCode <- StatusCodes.Status403Forbidden
                    Task.CompletedTask) |> ignore

        builder.Services.AddAuthorization() |> ignore

        let app = builder.Build()

        app.UseDefaultFiles() |> ignore
        app.UseStaticFiles() |> ignore
        app.UseAuthentication() |> ignore
        app.UseAuthorization() |> ignore

        use startupScope = app.Services.CreateScope()
        let startupDb = startupScope.ServiceProvider.GetRequiredService<AppDbContext>()
        startupDb.Database.EnsureCreated() |> ignore

        app.MapGet("/api/health", Func<IResult>(fun () ->
                Results.Ok {| status = "ok"; database = "PostgreSQL" |})) |> ignore


        app.MapPost("/api/auth/signup", Func<SignupRequest, HttpContext, Task<IResult>>(fun req ctx ->
            task {
                let name = if isNull req.username then "" else req.username.Trim()
                let pass = if isNull req.password then "" else req.password
                let selectedRole = if isNull req.role then "" else req.role.Trim()
                let selectedProject = if isNull req.project then "" else req.project.Trim()
                if name.Length < 3 || pass.Length < 8 then
                    return Results.BadRequest {| error = "Username must be at least 3 characters and password at least 8 characters." |}
                elif pass <> req.confirmPassword then
                    return Results.BadRequest {| error = "Passwords do not match." |}
                elif not (["Leader"; "Member"] |> List.contains selectedRole) then
                    return Results.BadRequest {| error = "Choose a valid role." |}
                elif not (["Music Cloud"; "AI Bot"; "Mining Project"] |> List.contains selectedProject) then
                    return Results.BadRequest {| error = "Choose a valid project." |}
                else
                    let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                    let! existing = db.Users.FirstOrDefaultAsync(fun u -> u.Username.ToLower() = name.ToLower() && u.Role = selectedRole && u.Project = selectedProject)
                    if not (isNull existing) then
                        return Results.Conflict {| error = "An account with this username already exists for this role and project." |}
                    else
                        let user = User(Username = name, PasswordHash = Security.hashPassword pass, Role = selectedRole, Project = selectedProject, GradePoints = 0)
                        db.Users.Add(user) |> ignore
                        let! _ = db.SaveChangesAsync()
                        do! signIn ctx user
                        return Results.Ok {| id = user.Id; username = user.Username; role = user.Role; project = user.Project |}
            })) |> ignore

        app.MapPost("/api/auth/login", Func<LoginRequest, HttpContext, Task<IResult>>(fun req ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                let name = if isNull req.username then "" else req.username.Trim().ToLower()
                let! user = db.Users.FirstOrDefaultAsync(fun u -> u.Username.ToLower() = name && u.Role = req.role && u.Project = req.project)
                if isNull user || not (Security.verifyPassword req.password user.PasswordHash) then
                    return Results.Unauthorized()
                else
                    do! signIn ctx user
                    return Results.Ok {| id = user.Id; username = user.Username; role = user.Role; project = user.Project |}
            })) |> ignore

        app.MapPost("/api/auth/logout", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                do! ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                return Results.Ok {| message = "Signed out." |}
            })) |> ignore

        app.MapGet("/api/auth/me", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                if not ctx.User.Identity.IsAuthenticated then return Results.Unauthorized()
                else
                    let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                    let! u = db.Users.FindAsync(userId ctx)
                    if isNull u then return Results.Unauthorized()
                    else return Results.Ok {| id = u.Id; username = u.Username; role = u.Role; project = u.Project; gradePoints = u.GradePoints |}
            })).RequireAuthorization() |> ignore


        app.MapGet("/api/team/members", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                if role ctx <> "Leader" then
                    return Results.Forbid()
                else
                    let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                    let p = project ctx

                    let! allUsers = db.Users.ToListAsync()

                    let members =
                        allUsers
                        |> Seq.filter (fun u -> u.Project = p && u.Role = "Member")
                        |> Seq.map (fun u -> {| id = u.Id; username = u.Username; gradePoints = u.GradePoints |})
                        |> Seq.toArray

                    return Results.Ok(members)
            })).RequireAuthorization() |> ignore


        app.MapPost("/api/tasks", Func<CreateTaskRequest, HttpContext, Task<IResult>>(fun req ctx ->
            task {
                if role ctx <> "Leader" then
                    return Results.Forbid()
                elif String.IsNullOrWhiteSpace(req.title) || req.assignedToId <= 0 || not ([24; 48; 72] |> List.contains req.hours) then
                    return Results.BadRequest {| error = "Enter a task, choose a member and select 24, 48 or 72 hours." |}
                else
                    let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                    let! memberUser = db.Users.FirstOrDefaultAsync(fun u -> u.Id = req.assignedToId && u.Role = "Member" && u.Project = project ctx)

                    if isNull memberUser then
                        return Results.BadRequest {| error = "That member is not part of this project." |}
                    else
                        let now = DateTime.UtcNow

                        let item = WorkTask(
                            Project = (project ctx),
                            Title = (req.title.Trim()),
                            Description = (if isNull req.description then "" else req.description.Trim()),
                            AssignedToId = memberUser.Id,
                            AssignedById = (userId ctx),
                            CreatedAt = now,
                            Deadline = (now.AddHours(float req.hours)),
                            Status = "Open",
                            Points = 10
                        )

                        db.Tasks.Add(item) |> ignore
                        let! _ = db.SaveChangesAsync()

                        return Results.Ok {| id = item.Id; title = item.Title; deadline = item.Deadline; points = item.Points |}
            })).RequireAuthorization() |> ignore


        app.MapGet("/api/tasks", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                let uid = userId ctx
                let! allTasks = db.Tasks.ToListAsync()

                let query =
                    if role ctx = "Leader" then
                        allTasks |> Seq.filter (fun t -> t.Project = project ctx && t.AssignedById = uid)
                    else
                        allTasks |> Seq.filter (fun t -> t.Project = project ctx && t.AssignedToId = uid)
                let items = query |> Seq.sortByDescending (fun t -> t.CreatedAt) |> Seq.toList
                let result =
                    items
                    |> List.map (fun t ->
                        {| id = t.Id
                           title = t.Title
                           description = t.Description
                           assignedToId = t.AssignedToId
                           assignedById = t.AssignedById
                           createdAt = t.CreatedAt
                           deadline = t.Deadline
                           status = t.Status
                           extensionRequested = t.ExtensionRequested
                           points = t.Points
                           completedAt = t.CompletedAt |})

                return Results.Ok(result)
            })).RequireAuthorization() |> ignore

        app.MapPost("/api/tasks/{id:int}/complete", Func<int, HttpContext, Task<IResult>>(fun id ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                let! item = db.Tasks.FirstOrDefaultAsync(fun t -> t.Id = id && t.Project = project ctx)
                if isNull item || item.AssignedToId <> userId ctx || role ctx <> "Member" then return Results.NotFound()
                elif item.Status = "Complete" then return Results.Ok {| points = item.Points; message = "Already completed." |}
                else
                    let now = DateTime.UtcNow
                    let lateHours = if now <= item.Deadline then 0 else int (Math.Floor((now - item.Deadline).TotalHours))
                    item.Points <- max 0 (10 - lateHours)
                    item.Status <- "Complete"
                    item.CompletedAt <- Nullable(now)
                    let! _ = db.SaveChangesAsync()
                    let! memberUser = db.Users.FindAsync(item.AssignedToId)
                    memberUser.GradePoints <- (db.Tasks.Where(fun t -> t.AssignedToId = memberUser.Id && t.Status = "Complete").Select(fun t -> t.Points).Sum())
                    let! _ = db.SaveChangesAsync()
                    return Results.Ok {| points = item.Points; lateHours = lateHours; totalGradePoints = memberUser.GradePoints |}
            })).RequireAuthorization() |> ignore

        app.MapPost("/api/tasks/{id:int}/request-extension", Func<int, HttpContext, Task<IResult>>(fun id ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                let! item = db.Tasks.FirstOrDefaultAsync(fun t -> t.Id = id && t.Project = project ctx)
                if isNull item || item.AssignedToId <> userId ctx || role ctx <> "Member" then return Results.NotFound()
                elif item.Status = "Complete" then return Results.BadRequest {| error = "This task is already complete." |}
                else
                    item.ExtensionRequested <- true
                    let! _ = db.SaveChangesAsync()
                    return Results.Ok {| message = "Extension request sent to your Team Leader." |}
            })).RequireAuthorization() |> ignore

        app.MapPost("/api/tasks/{id:int}/approve-extension", Func<int, ExtensionRequest, HttpContext, Task<IResult>>(fun id req ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                let! item = db.Tasks.FirstOrDefaultAsync(fun t -> t.Id = id && t.Project = project ctx && t.AssignedById = userId ctx)
                if isNull item || role ctx <> "Leader" then return Results.NotFound()
                elif not item.ExtensionRequested then return Results.BadRequest {| error = "No extension is pending." |}
                elif req.hours < 1 || req.hours > 72 then return Results.BadRequest {| error = "Choose between 1 and 72 extra hours." |}
                else
                    item.Deadline <- item.Deadline.AddHours(float req.hours)
                    item.ExtensionRequested <- false
                    let! _ = db.SaveChangesAsync()
                    return Results.Ok {| deadline = item.Deadline |}
            })).RequireAuthorization() |> ignore

        app.MapGet("/api/chat", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                let! messages = db.Messages.Where(fun m -> m.Project = project ctx).OrderBy(fun m -> m.CreatedAt).Take(250).ToListAsync()
                let result = messages |> List.ofSeq |> List.map (fun m ->
                    {| id = m.Id; senderId = m.SenderId; receiverId = m.ReceiverId; body = m.Body; audioUrl = m.AudioUrl; createdAt = m.CreatedAt |})
                return Results.Ok(result)
            })).RequireAuthorization() |> ignore

        app.MapPost("/api/chat", Func<ChatRequest, HttpContext, Task<IResult>>(fun req ctx ->
            task {
                if String.IsNullOrWhiteSpace(req.body) then return Results.BadRequest {| error = "Message cannot be empty." |}
                else
                    let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                    let msg = ChatMessage(Project = project ctx, SenderId = userId ctx, ReceiverId = req.receiverId, Body = req.body.Trim(), AudioUrl = "", CreatedAt = DateTime.UtcNow)
                    db.Messages.Add(msg) |> ignore
                    let! _ = db.SaveChangesAsync()
                    return Results.Ok {| id = msg.Id; createdAt = msg.CreatedAt |}
            })).RequireAuthorization() |> ignore

        app.MapPost("/api/chat/voice", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                let! form = ctx.Request.ReadFormAsync()
                let file = form.Files.GetFile("audio")
                if isNull file || file.Length = 0L then return Results.BadRequest {| error = "No audio file received." |}
                elif file.Length > 15L * 1024L * 1024L then return Results.BadRequest {| error = "Voice message must be under 15 MB." |}
                else
                    let uploads = Path.Combine(app.Environment.WebRootPath, "uploads")
                    Directory.CreateDirectory(uploads) |> ignore
                    let fileName = Guid.NewGuid().ToString("N") + ".webm"
                    let path = Path.Combine(uploads, fileName)
                    use stream = File.Create(path)
                    do! file.CopyToAsync(stream)
                    let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                    let msg = ChatMessage(Project = project ctx, SenderId = userId ctx, Body = "Voice message", AudioUrl = "/uploads/" + fileName, CreatedAt = DateTime.UtcNow)
                    db.Messages.Add(msg) |> ignore
                    let! _ = db.SaveChangesAsync()
                    return Results.Ok {| audioUrl = msg.AudioUrl |}
            })).RequireAuthorization() |> ignore

        app.MapGet("/api/grades", Func<HttpContext, Task<IResult>>(fun ctx ->
            task {
                let db = ctx.RequestServices.GetRequiredService<AppDbContext>()
                if role ctx = "Leader" then
                    let! members = db.Users.Where(fun u -> u.Project = project ctx && u.Role = "Member").Select(fun u -> {| id = u.Id; username = u.Username; gradePoints = u.GradePoints |}).ToListAsync()
                    return Results.Ok(members)
                else
                    let! u = db.Users.FindAsync(userId ctx)
                    return Results.Ok({| username = u.Username; totalGradePoints = u.GradePoints |})
            })).RequireAuthorization() |> ignore

        app.MapFallbackToFile("index.html") |> ignore
        app.Run()
        0
