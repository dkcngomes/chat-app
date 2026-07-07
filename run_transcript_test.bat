#!/bin/bash
# This is a Windows batch script for testing the transcript flow
@echo off
set T=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiRW1haWxUZXN0VXNlciIsImV4cCI6MTc4NDAxNzAyNywiaXNzIjoiQ2hhdEFwcCIsImF1ZCI6IkNoYXRBcHAifQ.iC4sIEqOzyIhfkkmsFJcKOIEAQixzzrrtDVhlwDEULY

echo [1/4] Start session...
curl -s -X POST http://localhost:5000/api/chat/sessions/start -H "Authorization: Bearer %T%" -H "Content-Type: application/json" -d "{}" -w " (%%{http_code})"
echo.

echo [2/4] Get rooms and verify General room exists...
curl -s http://localhost:5000/api/chat/rooms -H "Authorization: Bearer %T%" 
echo.

echo [3/4] End session (trigger transcript)...
curl -s -X POST http://localhost:5000/api/chat/sessions/end -H "Authorization: Bearer %T%" -H "Content-Type: application/json" -d "{}" -w " (%%{http_code})"
echo.

echo [4/4] Done! Background worker will pick this up within 60s.
echo Check chatlankainfo@gmail.com for the transcript email.
