A project similar to Songsterr. General concept:

* The administrator(s) should be able to upload a GuitarPro file to the page and, similar to Songsterr, display tabs for various instruments
* The ability to turn certain instruments on or off and adjust their volume
* Features for changing BPM, looping, metronome, etc.
* The ability to upload custom videos so they can be synchronized with the audio (this is a low priority).
* We need to come up with some kind of authorization system so the admin can approve or reject users. I was thinking of doing this via Telegram so the admin receives a notification and can confirm.

## Authentication and roles

The API uses **cookie authentication** based on ASP.NET Core Identity. The client never handles tokens: after a successful
sign-in the API sets an `HttpOnly` cookie (`MMS.Play.Auth`) and the browser sends it with every request. A cookie also
lets the Hangfire dashboard, a server-rendered page, be opened in an iframe or a new tab without extra work.

### Roles

| Role      | Can do                                                                         |
|-----------|--------------------------------------------------------------------------------|
| `Admin`   | Everything: upload and delete scores, Hangfire dashboard (`/hangfire`)          |
| `Student` | Read only: list scores, open score details and download the file to play it    |

Rules:

* Every endpoint requires a signed-in user with a role (fallback authorization policy). A user without a role gets `403`.
* Admin-only actions are marked with `[Authorize(Roles = Roles.Admin)]`; role names live in `MMS.Play.Contracts.Authorization.Roles`.
* Anonymous endpoints: `POST /api/v1/account/login`, `POST /api/v1/account/logout`, `/health/live`, `/health/ready`.
* Unauthenticated requests get `401`, forbidden ones `403`, both as problem details (no redirects to a login page).

### Endpoints

| Method | Route                     | Description                                                              |
|--------|---------------------------|--------------------------------------------------------------------------|
| POST   | `/api/v1/account/login`   | `{ "userName", "password", "rememberMe" }`, sets the cookie, returns the user |
| POST   | `/api/v1/account/logout`  | Removes the cookie                                                       |
| GET    | `/api/v1/account/me`      | Current user `{ id, userName, roles }`, `401` when not signed in          |

`rememberMe: true` issues a persistent cookie (14 days, renewed while the user is active), otherwise the cookie lives until
the browser is closed. Five failed attempts in a row lock the account for five minutes.

### Using it from the client

* Call the API from the same site: in development through the Angular proxy (`proxy.conf.json`, `/api` -> API),
  in production through a reverse proxy on the same domain. The cookie is `SameSite=Strict`, which also protects against CSRF.
* On startup call `GET /api/v1/account/me`: `200` restores the session, `401` means show the login page.
* Use `roles` from the response to show or hide admin features. This is cosmetic only, the API enforces the rules itself.
* To show Hangfire inside the client, add `/hangfire` to the proxy as well and embed it in an iframe.
* Swagger UI works too: call `login` once and the following requests are sent with the cookie.

### Accounts

There is no registration yet. Roles are created on every startup, accounts come from `Identity:SeedUsers` and are created
only when they do not exist (an existing account keeps its password):

```json
"Identity": {
  "SeedUsers": [
    { "UserName": "admin", "Password": "Admin123!", "Role": "Admin" },
    { "UserName": "student", "Password": "Student123!", "Role": "Student" }
  ]
}
```

`appsettings.Development.json` contains the two accounts above. In other environments keep the passwords out of
appsettings (user secrets or environment variables, e.g. `Identity__SeedUsers__0__Password`).
Passwords need at least 8 characters with an upper case letter, a lower case letter, a digit and a symbol.

Planned: self-registration with admin approval through a Telegram bot. A registered user without a role can already sign in
but gets `403` everywhere, so approving a user will mean assigning the `Student` role.
