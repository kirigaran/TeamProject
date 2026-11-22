# TeamSpace — .NET 10 + F# + SQLite

A starter full-stack project workspace with a warm cream/light-brown visual theme.

## Included

- Welcome page and project selection: Music Cloud, AI Bot, Mining Project
- Separate Team Leader / Team Member accounts for each project
- Signup and login with PBKDF2 password hashing and HTTP-only authentication cookies
- SQLite database (`teamproject.db`) created automatically
- Team Leader task assignment to a project member
- 24 / 48 / 72 hour deadlines; new tasks start at 10 points
- Member task completion and extension requests
- Leader approval of an extra 24 hours
- Late completion score: one point deducted per completed hour after the deadline, minimum zero
- Team chat with polling and browser voice recording/upload
- Grade totals and team member overview

## Requirements

- .NET 10 SDK
- VS Code
- C# Dev Kit is not required; install the Ionide-fsharp extension for F# editing.

## Run in VS Code

1. Extract the ZIP.
2. Open the `TeamProject` folder in VS Code.
3. Open Terminal → New Terminal.
4. Run:

   ```bash
   dotnet restore
   dotnet run
   ```

5. Open the local URL printed in the terminal (usually `http://localhost:5000` or `https://localhost:7000`).

The SQLite database is created in the project folder on first run.

## Important notes before real deployment

- The forgot-password screen is intentionally a safe placeholder. Configure verified email/SMS OTP recovery before allowing password resets. A username-only reset would let someone take over another person's account.
- The current chat refreshes every 15 seconds. For instant live chat, add SignalR in a later iteration.
- Browser notifications are not pushed while the browser is fully closed. Production push notifications require a service worker and push provider.
- Voice messages are stored under `wwwroot/uploads`. Configure file retention, size limits, malware scanning and private file access before public deployment.
- The app uses cookie authentication and is intended to run over HTTPS in production. Configure secure cookies, rate limiting, backups, logging and database migrations before public launch.
- Grade deductions are calculated when a task is marked complete, using the server clock. The sample deducts one point per *full hour* elapsed after the deadline.
- This starter does not yet include email verification, invitation codes, project membership approval, or production-grade real-time delivery.

## Account rules

A username is unique within a selected project and role. The same username may be used for a different role or project. A Team Leader and Team Member must choose the same project to work together.

## Folder layout

```text
TeamProject/
├── TeamProject.fsproj
├── Program.fs
├── README.md
└── wwwroot/
    ├── index.html
    ├── styles.css
    └── app.js
```
