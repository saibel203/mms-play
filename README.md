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

## Telegram bot

The bot receives updates with long polling (`TelegramPollingService`), so it runs on a dev machine without a public address.
Only one running instance may poll a bot: use a separate bot for development.

| Command / event       | Effect                                                                    |
|-----------------------|---------------------------------------------------------------------------|
| `/start`              | Creates or reactivates a row in `TelegramSubscribers`                     |
| `/start inv_…`        | The same, from an invite link: gives the invite's role (student / parent with children) |
| `/stop`               | Deactivates the subscriber                                                |
| `/lessons`            | The next 5 lessons, each with a «Перенести» button                        |
| `/payments`           | Unpaid lessons that took place: the student's own, or for a parent each child's |
| `/homework`           | Active homework with what to do, days in a row, «✅ Позаймався» buttons and the practice page link, see "Homework" |
| `/pay`                | The amount owed, the payment details and a «💳 Оплатити» URL button (notification settings `paymentLink` / `paymentDetails`) |
| `/calendar`           | The personal calendar link (iCalendar) with how to add it to Google / Apple Calendar, and «🔄 Нове посилання», see "Lesson calendar" |
| `/trial`              | Newcomers only: book a trial lesson (day → time → confirm, or "any time"), see "Trial lessons" |
| Menu keyboard         | «📅 Мій розклад» = `/lessons`, «💳 Мій борг» = `/payments`, «💰 Оплатити» = `/pay`, «📚 Домашнє завдання» = `/homework`, «📆 Календар» = `/calendar`, «❓ Допомога» = help, and «🎸 Пробний урок» = `/trial` on top for newcomers only; built per user (`BotMenu.KeyboardFor`, `TrialEligibility`), sent with `/start`, help and the bot's answers, removed by `/stop`. The command list behind Telegram's «Меню» is registered with `setMyCommands` when polling starts |
| `/clear`              | Deletes the last 1000 messages of the chat (bot's and user's) younger than 48 hours; older ones only the user can clear in the app |
| Buttons               | The reschedule flow, see "Rescheduling in the bot"; answers to parent questions, see "Parents and broadcasts" |
| Any other text        | A parent's typed answer to a question, see "Parents and broadcasts"      |
| User blocks the bot   | Deactivates the subscriber (also when a send returns `403`)               |

Subscribers are not linked to application accounts yet.

Students are reminded of their lessons (see "Lessons" below). The template is Telegram HTML with the placeholders
`{FirstName}`, `{LessonDay}` ("сьогодні", "завтра", "у середу (12.11)"), `{LessonDate}` ("середа, 12.11"), `{LessonTime}`, `{Room}` and `{Teacher}`. The template saved by an admin
is kept in the `MessageTemplates` table, `LessonReminders:MessageTemplate` is the default until then. A reminder goes
only to students who are subscribed in the bot and have reminders enabled by an admin.

### Admin endpoints

Admins only, they work while the bot is turned off too.

| Method | Route                                                | Description                                                         |
|--------|------------------------------------------------------|---------------------------------------------------------------------|
| GET    | `/api/v1/telegram/subscribers`                       | `?page&pageSize&search&isSubscribed`, paged list, latest first      |
| PUT    | `/api/v1/telegram/subscribers/{id}/reminders`        | `{ "enabled": true }`, the reminder checkbox, returns the subscriber |
| PUT    | `/api/v1/telegram/subscribers/{id}/admin`            | `{ "isAdmin": true }`: gets reschedule requests in the bot          |
| PUT    | `/api/v1/telegram/subscribers/{id}/parent`           | `{ "isParent": true, "childIds": [] }`: parent role and children    |
| GET    | `/api/v1/telegram/templates`                         | Every bot message text: `[{ kind, text, isDefault, updatedAt, placeholders }]` |
| PUT    | `/api/v1/telegram/templates/{kind}`                  | `{ "text" }`, `400` with `errors.Text` when the markup is invalid |
| DELETE | `/api/v1/telegram/templates/{kind}`                  | Back to the appsettings default, returns it                         |
| GET    | `/api/v1/telegram/settings`                          | Notification settings: lesson / payment / homework reminders, payment link and details, daily summary, trial booking |
| PUT    | `/api/v1/telegram/settings`                          | Saves them, `400` with the field when a value is out of range        |
| POST   | `/api/v1/telegram/digest/send-now`                   | Today's admin summary in the bot now, `{ "sent": 2 }`; `400` without a subscribed admin or with the bot off |

Message kinds (`{kind}`): `LessonReminder`, `ParentLessonReminder`, `PaymentReminder`, `ParentPaymentReminder`,
`HomeworkAssigned`, `HomeworkReminder` (defaults `HomeworkReminders:AssignedMessageTemplate` / `ReminderMessageTemplate`).
The defaults are `LessonReminders:MessageTemplate` / `ParentMessageTemplate` and `PaymentReminders:StudentMessageTemplate` /
`ParentMessageTemplate`; a saved text is kept in `MessageTemplates` under the kind's name. A text is checked before it is
saved: Telegram rejects a message with broken HTML, a bad template would fail every message of its kind.

### Cancelling lessons

A lesson that has not started can be cancelled by an admin on the website (`POST /lessons/{id}/cancel` with an optional
reason) or in the bot by its student or a parent of the student: «❌ Скасувати» under a reminder or in `/lessons`, then a
reason button. It becomes `Cancelled`: it keeps its place in the timetable, crossed out, and counts nowhere (earnings,
debts, reminders, clash checks); a pending reschedule request is closed. An admin's cancellation is sent to the student
and the parents, a family's to the bot admins. Less than 24 hours before the start it is late (`IsLateCancellation`,
decided when cancelled). `POST /lessons/{id}/restore` brings it back when its time is still free. The earnings
statistics count cancellations per period and teacher, late ones, by whom, and the students who cancel the most.

### Invite links

`GET/POST /telegram/invites`, `DELETE /telegram/invites/{id}`. An invite is `https://t.me/{BotUsername}?start=inv_{code}`
(16 random URL safe characters): whoever opens it and presses Start is subscribed and gets its role, a parent linked to
the invite's children. Personal (`maxUses: 1`) or shared (no limit), optionally expiring. Needs `Telegram:BotUsername`.

### Payments

A lesson is owed once it has started and is not paid (free lessons never are); unpaid lessons still to come are not a
debt yet. `PaymentReminders` in appsettings holds the defaults of the schedule (`Enabled`, `Time`, `EveryDays`), the
notification settings on the Telegram page override them. At that time of day the reminder job sends one message per student who
owes to the student and to each parent (with «Reminders» on), listing the lessons (`{UnpaidLessons}`, `{UnpaidCount}`,
`{UnpaidAmount}`), and again every `EveryDays` while the lessons stay unpaid. `PaymentReminderDeliveries` logs every
message, the «Remind» button on the website included.

Where to pay is set in the notification settings: `paymentLink` (an absolute http(s) URL, e.g. a Monobank jar) and
`paymentDetails` (plain text: recipient, IBAN, purpose). The link becomes a «💳 Оплатити» URL button under payment
reminders and the `/payments` and `/pay` answers; without a link the details are appended to reminders (unless the
template shows them itself). Templates can also use `{PaymentLink}` and `{PaymentDetails}`, empty when not set.

| Method | Route                                         | Description                                                    |
|--------|-----------------------------------------------|----------------------------------------------------------------|
| GET    | `/api/v1/lessons/payments?from&to`            | Per student: paid, owed, upcoming, the lessons, last reminder  |
| PUT    | `/api/v1/lessons/payments`                    | `{ "lessonIds": [], "isPaid": true }`, returns `{ changed }`   |
| POST   | `/api/v1/lessons/payments/{studentId}/remind` | A payment reminder now, `400` if nothing is owed or nobody can get it |
| POST   | `/api/v1/lessons/payments/remind-all` | A payment reminder about every debtor now; `{ debtors, notReachable, sent, failed, unsubscribed }`, `400` if nobody owes |

### Homework

An admin gives a student homework on the website (Lessons → Homework): a score from the library (optional), the bars
(`fromBar`/`toBar`, 1-based) and tempo (`startTempoPercent` → `targetTempoPercent`, percent of the original), what to do
and a deadline. The student gets the `HomeworkAssigned` message in the bot right away (also with «Reminders» off; a
message that does not go out does not fail the request). `/homework` or «📚 Домашнє завдання» shows the active homework
(a parent: each child's) with the days in a row; «✅ Позаймався» (callback `hw:done:<id>`) marks it practised today,
one `HomeworkPractices` row per homework and school day.

Every day at the homework reminder time (notification settings, `HomeworkReminders:Enabled` / `Time` until saved) the
reminder job sends one `HomeworkReminder` message to every student (subscribed, «Reminders» on, not a parent) with
active homework not marked practised today: `{Homework}` lists it (at most `HomeworkReminders:MaxListed`, with a
«Позаймався» button each), `{Streak}` is the days in a row, `{PracticeLink}` the practice page. `HomeworkReminderDeliveries`
makes it once a day. Completed homework is no longer shown or reminded about.

The practice page `{PublicSite:BaseUrl}/practice/{token}` (the subscriber's `AccessToken`, created when first needed)
opens without signing in: the active homework of the link owner and of their children, the score in the player, a
«practised» button. Without `PublicSite:BaseUrl` the bot sends no link. Telegram takes the link as a button only for a
public https address; otherwise it is written into the text.

| Method | Route                                                      | Description                                                  |
|--------|------------------------------------------------------------|--------------------------------------------------------------|
| GET    | `/api/v1/homework?studentId&status`                        | Admin: homework with practice days, streak, practised today   |
| POST   | `/api/v1/homework`                                         | Admin: `{ studentId, scoreId?, title?, instructions?, fromBar?, toBar?, startTempoPercent?, targetTempoPercent?, dueDate? }`, `201`; the title defaults to the score's |
| PUT    | `/api/v1/homework/{id}`                                    | Admin: the same fields                                       |
| PUT    | `/api/v1/homework/{id}/status`                             | Admin: `{ "status": "Completed" }` or `"Active"`              |
| DELETE | `/api/v1/homework/{id}`                                    | Admin: with its practice marks                               |
| GET    | `/api/v1/practice/{token}`                                 | Anonymous: `{ name, homework: [...] }`, `404` for an unknown link |
| GET    | `/api/v1/practice/{token}/homework/{id}/score`             | Anonymous: the score file of an active homework              |
| POST   | `/api/v1/practice/{token}/homework/{id}/practiced`         | Anonymous: marks it practised today, returns the homework    |

### Parents and broadcasts

A subscriber marked as a parent (`IsParent`) is linked to their children, students (subscribers who are not parents),
in `TelegramSubscriberChildren`. A child may have several parents. Parents are left out of the lesson student list.

Parents with «Reminders» on get the lesson reminders of their children, on the same schedule, with
`LessonReminders:ParentMessageTemplate` (the reminder placeholders, `{FirstName}` being the parent, plus `{ChildName}`)
and without the reschedule button. The child does not have to use the bot. A parent is never reminded as a student,
even when set as the student of a lesson. `LessonReminderDeliveries` is keyed by lesson, recipient and slot.

An admin sends a broadcast from the website to:
- `AllParents`: every parent, one message each; `{ChildName}` is all their children;
- `LessonDay`: the parents of the students with a lesson on a day, one message per lesson; the lesson placeholders
  of the reminder (`{LessonDay}`, `{LessonDate}`, `{LessonTime}`, `{Room}`, `{Teacher}`) work too.

The admin unticks whoever should not get it. A broadcast is a plain message or a question: answer buttons (`options`),
a typed answer (`allowTextAnswer`) or both. A question with a typed answer only opens the reply field in Telegram
(`ForceReply`). A typed message answers the question it replies to, otherwise the latest unanswered question of the
chat from the last 7 days; a message that answers nothing gets the usual help. The last answer wins, the question
message shows it and ticks the pressed button.

`ParentBroadcasts` keeps what was sent, `ParentBroadcastDeliveries` one row per message with the filled in text,
the child and lesson copied (they stay readable when the lesson changes) and the answer. Sending runs inside the
`POST` request, about 25 messages per second, and is not stopped when the admin closes the page. With the bot turned
off a broadcast is refused (`400`).

| Method | Route                                       | Description                                                      |
|--------|---------------------------------------------|------------------------------------------------------------------|
| GET    | `/api/v1/broadcasts/recipients`             | `?audience=LessonDay&lessonDate=2026-11-12`, who would get it   |
| POST   | `/api/v1/broadcasts`                        | Sends it, `201` with every message                               |
| GET    | `/api/v1/broadcasts`                        | `?page&pageSize`, sent broadcasts with delivery and answer counts |
| GET    | `/api/v1/broadcasts/{id}`                   | Every message and answer                                         |

### Setup

1. Create the bot in [@BotFather](https://t.me/BotFather) with `/newbot`, it returns the token.
2. Keep the token out of appsettings:
   ```
   dotnet user-secrets init --project src/MMS.Play.Web
   dotnet user-secrets set "Telegram:BotToken" "<token>" --project src/MMS.Play.Web
   ```
   In other environments use the `Telegram__BotToken` environment variable.
3. Set `Telegram:BotUsername`, `Telegram:Enabled: true` and, for reminders, `Jobs:LessonReminders:Enabled: true`.
   The job needs the bot enabled, otherwise it fails.
4. Open the bot in Telegram and press Start.

The URL of every Bot API call contains the token: keep `System.Net.Http.HttpClient` logging at `Warning` or above.

## Lessons

The timetable of the school: rooms with their hours per weekday, teachers (each with a colour for the UI) and lessons.
A lesson has a date, a start time (lessons last 60 minutes), a room, a teacher, a student (a Telegram subscriber) and/or a
note (e.g. a trial lesson of someone not in the bot yet), a price and a "paid" flag. Admins only.

* **School time.** Dates (`DateOnly`) and times (`TimeOnly`) are the school's local time, `Lessons:TimeZoneId`
  (`Europe/Kyiv`). "Today" and the reminder times are taken in that zone whatever the server's own zone is.
* **No clashes.** A room, a teacher and a student are in one lesson at a time: another lesson of the same room, teacher or
  student starting less than 60 minutes before or after is refused with `400`.
* **Prices.** A new lesson gets the default price (`500` from `Lessons:DefaultLessonPrice` until an admin saves another one)
  and keeps it: changing the default never rewrites past earnings. Earned = sum of the prices of the lessons marked as paid.
* **Reference data.** Teachers (Рома, Настя, Аня, Діана) and rooms (314, 305, 104 with their hours) are created on startup
  when their tables are empty (`LessonDataSeeder`). There is no UI to edit them yet.
* **Demo data.** With `Lessons:SeedDemoData` (on in Development) an empty timetable is filled with 31 demo students and
  about three months of lessons back plus two weeks ahead, most past ones paid. Demo students have fake Telegram ids
  (from `4000000000000000`) and reminders off, so the bot never messages them. To start over, delete the rows of
  `Lessons` and restart.

### Reminders

From `ReminderDaysBefore` days before a lesson until it starts, at each of `ReminderTimes` (school time), the student gets
one Telegram message per lesson. Defaults: 2 days, 09:00 and 13:00 (`LessonReminders:*`); an admin changes them on the
notification settings (Telegram page). The `send-lesson-reminders` Hangfire job runs every minute (`Jobs:LessonReminders:Schedule`)
and serves a reminder time once it has passed, at most `LessonReminders:MissedSlotGraceMinutes` (120) late, e.g. after a
restart; older ones are skipped instead of being sent hours late. `LessonReminderDeliveries` records what was sent per
lesson and time, so a reminder is never sent twice.

**Testing mode:** with an interval (`LessonReminders:IntervalMinutes`, or "Every N min" in the notification settings) reminders go
out every N minutes instead of at the times. `appsettings.Development.json` sets it to 1 minute; it applies only while the
notification settings have not been saved on the website (saved settings carry their own mode).

Every reminder has a «🔁 Перенести урок» button, see below.

### Notification settings

Everything about when the bot writes on its own is one form on the Telegram page (`GET/PUT /api/v1/telegram/settings`,
`NotificationSettingsService`): lesson reminders (on, days before, times or a test interval), payment reminders (on, time,
every N days), the payment link and details, homework reminders (on, time), the admins' daily summary (on, time) and
trial lesson booking (on). The values live in the single `SchoolSettings` row next to the default lesson price (which
stays in `/api/v1/lessons/settings`); whichever of the two is saved first creates the row with the other's appsettings
defaults (`SchoolSettingsDefaults`), and neither save touches the other's columns. Until then the defaults come from
`LessonReminders`, `PaymentReminders`, `HomeworkReminders`, `AdminDigest` and `TrialBooking` in appsettings.

### Daily summary for admins

At the digest time (`AdminDigest:Enabled` / `Time`, 08:00, until the notification settings are saved) the reminder job
sends every subscribed bot admin (the «Admin» checkbox) one message: today's lessons (time, student, teacher, room;
cancelled ones crossed out), cancellations of the last 24 hours (late ones marked), unanswered reschedule requests, new
trial requests, the total debt with the number of debtors, and how many students marked homework practised yesterday,
plus a «🗓 Відкрити розклад» button (or the link as text) when `PublicSite:BaseUrl` is set. An `AdminDigestDeliveries`
row (one per school date) is inserted before sending, so the summary goes out once a day even with two job runs; like
the reminders it is sent at most `LessonReminders:MissedSlotGraceMinutes` late. «Send now» on the Telegram page
(`POST /api/v1/telegram/digest/send-now`) sends it at once without counting as the day's summary.

### Rescheduling in the bot

1. The student presses «Перенести урок» under a reminder or in `/lessons` (the next 5 lessons).
2. The bot shows the days of the next `Lessons:RescheduleDaysAhead` (14) days that have free hours, then the free hours
   of the picked day. Free = the teacher and the student have no lesson and a room is open and free; the lesson's own room
   is preferred. Hours earlier than `Lessons:RescheduleMinNoticeHours` (2) from now are not offered. Times an admin already
   declined for this lesson are shown crossed out (`❌ 1̶6̶:̶0̶0̶`) and cannot be picked again.
3. The proposal (`LessonRescheduleRequests`, status `Pending`) goes to every **bot admin** (the «Admin» checkbox on the
   Telegram page) with «✅ Так» / «❌ Ні». The student can withdraw it until it is answered.
4. **Yes:** the old lesson becomes `Rescheduled` (kept, crossed out on the website, counted nowhere), a new lesson takes
   the proposed time with the price and the payment. **No:** the student gets a new message with the free times again.
   If the time got taken in the meantime, a "yes" turns into "no" with an explanation. The first admin to answer wins,
   the others' messages lose their buttons. The student is told in a new message, as edits do not notify.
5. Requests can also be answered on the timetable (lessons marked ⏳), which works without any bot admin.

Callback data of the buttons is described in `RescheduleView`.

### Trial lessons

Newcomers ask for a trial lesson in the bot («🎸 Пробний урок», `/trial`) or on the public page
`{PublicSite:BaseUrl}/trial` (no sign-in). Requests are stored in `TrialRequests` (`New` → `Booked` / `Declined`).

* **Who sees the bot button.** Nobody switches it by hand: `TrialEligibility` decides from the data every time the menu
  is sent. The button is there for an active subscriber who **never had a lesson as a student** (any status, cancelled
  too), is **not a parent with children**, **not an admin**, has **no request `New` or `Booked`**, while
  `trialBookingEnabled` is on in the notification settings (`TrialBooking:Enabled` until saved). So it disappears as soon
  as the person sends a request, gets any lesson (also one an admin books by hand), or is made a parent by an invite;
  after a decline it comes back. `/trial` typed by someone else gets a polite explanation instead.
* **Free times** (`TrialSlots`): the next `TrialBooking:DaysAhead` (14) days, from `TrialBooking:MinNoticeHours` (24)
  from now. A time is free when a room is open then (its hours) and free, and a teacher has no lesson then (the 60 min
  rule); requests still `New` hold their time, so two newcomers are not promised the last room.
* **Bot flow** (callbacks `trial:…`, `TrialView`): days → times → «✅ Так, надіслати заявку», or «📞 Будь-який час».
  Name and `@username` come from Telegram. A new message then replaces the menu (without the trial button).
* **Public page**: name, phone or @telegram, comment, a picked time or "call me". Spam guards: a hidden `website` field
  (filled = accepted and dropped), at most `TrialBooking:MaxWebsiteRequestsPerHour` (10) website requests an hour,
  one waiting request per contact.
* **Admins** (bot subscribers with «Admin») get every request in the bot. On the website (Lessons → Trial requests)
  «Book» creates the lesson through the usual lesson service (validation, clash checks; price empty = default): for
  a bot request the subscriber is the student and is told in the bot (with a fresh menu), for a website request the
  lesson has no student and the note «Пробний: name, contact». «Decline» tells a bot requester, with an optional reason.

| Method | Route                                     | Description                                                       |
|--------|-------------------------------------------|-------------------------------------------------------------------|
| GET    | `/api/v1/trial/slots`                     | Anonymous: `{ isOpen, days: [{ date, times: ["15:00:00"] }] }`     |
| POST   | `/api/v1/trial/requests`                  | Anonymous: `{ name, contact, comment?, date?, time?, website? }`, `202`; `400` when closed, invalid, taken or too many |
| GET    | `/api/v1/trial/requests?status`           | Admin: newest first, `New` / `Booked` / `Declined` or all          |
| POST   | `/api/v1/trial/requests/{id}/book`        | Admin: `{ teacherId, roomId, date, startTime, price? }` → the request with `lessonId`; `400` answered / clash |
| POST   | `/api/v1/trial/requests/{id}/decline`     | Admin: `{ reason? }`                                              |

### Lesson calendar

`/calendar` («📆 Календар») gives the subscriber a personal iCalendar link
`{PublicSite:BaseUrl}/api/v1/calendar/{token}.ics` (the subscriber's `AccessToken`, shared with the practice page link)
and how to subscribe to it: Apple (Settings → Calendar → Accounts → Add Subscribed Calendar, or the `webcal://` link)
and Google Calendar (Other calendars → From URL, on a computer). «🔄 Нове посилання» (after a confirmation) replaces the
token: the old calendar and practice links stop working. Without `PublicSite:BaseUrl` the bot says the calendar is not
available yet.

`GET /api/v1/calendar/{token}.ics` is anonymous (calendar apps cannot sign in), `404` for an unknown token or an
unsubscribed person. It is built on every request (`CalendarFeedService`, hand-written `IcsWriter`: CRLF, escaping,
folding at 75 octets): the lessons of the subscriber and, for a parent, of their children, 30 days back and 180 ahead;
times converted from school time to UTC; one event per lesson with a stable `UID` (the lesson id), so a moved lesson
moves; cancelled ones stay as `STATUS:CANCELLED`, rescheduled ones are left out (their replacement is a new event); a
reminder an hour before. `REFRESH-INTERVAL` / `X-PUBLISHED-TTL` ask for an hourly refresh (Google refreshes on its own
schedule, often every few hours).

### Endpoints

| Method | Route                              | Description                                                               |
|--------|------------------------------------|---------------------------------------------------------------------------|
| GET    | `/api/v1/lessons/week?date`        | Rooms with hours, teachers and lessons of the week containing `date`      |
| POST   | `/api/v1/lessons/week/copy-previous` | Copies the week before into the week of `date`; skips existing, past and clashing lessons |
| GET    | `/api/v1/lessons/students`         | Every Telegram subscriber by name, for picking the student of a lesson    |
| POST   | `/api/v1/lessons`                  | Creates a lesson; `repeatWeeks` creates a weekly series (shared `seriesId`), all or none |
| PUT    | `/api/v1/lessons/{id}`             | Changes a lesson; `"scope": "Following"` also the later lessons of its series, all or none |
| PUT    | `/api/v1/lessons/{id}/payment`     | `{ "isPaid": true }`                                                      |
| DELETE | `/api/v1/lessons/{id}?scope`       | Deletes a lesson; `scope=Following` ends its series from today on        |
| GET    | `/api/v1/lessons/stats?date`       | Lessons and earnings of the day, week, month, quarter and year, by teacher |
| GET    | `/api/v1/lessons/settings`         | The default lesson price (the bot's schedule: `/api/v1/telegram/settings`) |
| PUT    | `/api/v1/lessons/settings`         | `{ "defaultLessonPrice": 500 }`                                           |
| POST   | `/api/v1/lessons/reschedule-requests/{id}/approve` | Moves the lesson; `{ "decision": "Approved" }` or `"SlotTaken"`, `409` if answered |
| POST   | `/api/v1/lessons/reschedule-requests/{id}/decline` | The student is asked to pick another time                       |
| GET    | `/api/v1/teachers`                 | Teachers with their lesson counts                                         |
| POST / PUT | `/api/v1/teachers[/{id}]`      | `{ "name", "color": "#22c55e" }`, unique name                             |
| DELETE | `/api/v1/teachers/{id}`            | Only without lessons, `400` otherwise                                     |
| GET    | `/api/v1/rooms`                    | Rooms with hours (Monday first) and lesson counts                         |
| POST / PUT | `/api/v1/rooms[/{id}]`         | `{ "name", "hours": [{ "dayOfWeek": "Monday", "firstLessonAt": "10:00:00", "lastLessonAt": "20:00:00" }] }`, a day left out is closed |
| DELETE | `/api/v1/rooms/{id}`               | Only without lessons and reschedule requests, `400` otherwise             |
