@echo off
set TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjQiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiVHJhbnNjcmlwdFRlc3QiLCJleHAiOjE3ODQwMTY0MjksImlzcyI6IkNoYXRBcHAiLCJhdWQiOiJDaGF0QXBwIn0.N8FzuzDhirYYDSDtcBDnWy_tBnbPBAgAZQuCI97dsos

echo === Step 1: Start a session ===
curl -s -X POST http://localhost:5000/api/chat/sessions/start -H "Authorization: Bearer %TOKEN%" -H "Content-Type: application/json" -d "{}"
echo.

echo === Step 2: Send a message to General room (room 1) ===
curl -s -X POST http://localhost:5000/api/chat/rooms -H "Authorization: Bearer %TOKEN%" -H "Content-Type: application/json" -d "{\"name\":\"TestRoom\",\"memberIds\":[]}"
echo.

echo === Step 3: Send a few text messages ===
curl -s http://localhost:5000/api/chat/rooms -H "Authorization: Bearer %TOKEN%"
echo.

echo === Step 4: End the session ===
curl -s -X POST http://localhost:5000/api/chat/sessions/end -H "Authorization: Bearer %TOKEN%" -H "Content-Type: application/json" -d "{}"
echo.

echo === Step 5: Check if session is marked as ended ===
curl -s http://localhost:5000/api/chat/rooms -H "Authorization: Bearer %TOKEN%"
echo.
