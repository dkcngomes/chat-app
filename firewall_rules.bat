@echo off
echo ========================================
echo  Adding Chat App Firewall Rules
echo ========================================
echo.

:: Frontend (Vite dev server on port 5173)
netsh advfirewall firewall add rule name="Chat App - Frontend (5173)" dir=in action=allow protocol=TCP localport=5173 profile=private,domain description="Chat App Vite dev server"

:: Backend (.NET API on port 5000)
netsh advfirewall firewall add rule name="Chat App - Backend API (5000)" dir=in action=allow protocol=TCP localport=5000 profile=private,domain description="Chat App .NET backend"

echo.
echo Done! Check the results above.
pause
