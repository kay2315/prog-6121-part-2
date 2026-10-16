# RaceDay Part 2 — manual endpoint verification checklist

Use Swagger (`/swagger`) after the API is running. Keep the same browser session between login and protected requests.

1. Register a Participant with a valid email and password of at least 8 characters. Expected: `201 Created`.
2. Register the same email again. Expected: `409 Conflict`.
3. Log in with the correct password. Expected: `200 OK`; session cookie should be retained.
4. Log in with a wrong password. Expected: `401 Unauthorized`.
5. Call `GET /api/users/me` without logging in. Expected: `401 Unauthorized`.
6. Log in as Participant and call `POST /api/events`. Expected: `403 Forbidden`.
7. Log in as Organiser and create an event. Expected: `201 Created`.
8. Attempt to edit an event owned by another Organiser. Expected: `403 Forbidden`.
9. View the event list and filter by city/event type. Expected: `200 OK` and matching events.
10. Add a category to an event as its Organiser. Expected: `201 Created`.
11. Enrol a Participant in a valid category. Expected: `201 Created`.
12. Enrol the same Participant in the same category again. Expected: `409 Conflict`.
13. Attempt enrolment as Organiser. Expected: `403 Forbidden`.
14. View enrolments as the owning Organiser. Expected: `200 OK`.
15. Record a result for a valid enrolment as the owning Organiser. Expected: `201 Created`.
16. View event results and the Participant's own results. Expected: `200 OK`.
17. Try to modify another user's enrolment or another organiser's result. Expected: `403 Forbidden`.
18. Log out and retry a protected endpoint. Expected: `401 Unauthorized`.

Record actual results and screenshots for the report. Do not claim a test passed unless you ran it and observed the expected response.
