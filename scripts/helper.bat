@echo off
REM ============================================================================
REM MQTT SUBSCRIBER HELPER
REM Starts mosquitto_sub to listen for SOMIOD notifications
REM ============================================================================

echo.
echo ========================================
echo   SOMIOD MQTT SUBSCRIBER
echo ========================================
echo.
echo This script will start listening for MQTT notifications
echo from your SOMIOD middleware.
echo.
echo Choose your MQTT broker:
echo [1] localhost (requires local mosquitto broker)
echo [2] test.mosquitto.org (free public broker)
echo.
choice /C 12 /M "Select option"

if errorlevel 2 goto public
if errorlevel 1 goto local

:local
echo.
echo Starting subscriber on localhost...
echo Topic: api/somiod/#
echo.
echo Press Ctrl+C to stop
echo.
mosquitto_sub -h localhost -t "api/somiod/#" -v
goto end

:public
echo.
echo Starting subscriber on test.mosquitto.org...
echo Topic: api/somiod/#
echo.
echo Press Ctrl+C to stop
echo.
mosquitto_sub -h test.mosquitto.org -t "api/somiod/#" -v
goto end

:end
pause