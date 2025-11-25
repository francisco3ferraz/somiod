@echo off
REM ============================================================================
REM SOMIOD - QUICK NOTIFICATION TEST
REM Tests the notification system with minimal setup
REM ============================================================================

setlocal EnableDelayedExpansion

set BASE_URL=https://localhost:44346/
set API_URL=%BASE_URL%/api/somiod

echo.
echo ========================================
echo   SOMIOD QUICK NOTIFICATION TEST
echo ========================================
echo.
echo PREREQUISITES:
echo 1. SOMIOD middleware is running at %BASE_URL%
echo 2. Mosquitto broker is running (local or test.mosquitto.org)
echo 3. Run this in one terminal: mosquitto_sub -h localhost -t "api/somiod/#" -v
echo    OR: mosquitto_sub -h test.mosquitto.org -t "api/somiod/#" -v
echo.
pause

REM Use localhost or test.mosquitto.org
set MQTT_BROKER=localhost
REM set MQTT_BROKER=test.mosquitto.org

echo.
echo [1/6] Creating Application...
curl -X POST "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"test-app\"}"
echo.
timeout /t 2 /nobreak >nul

echo.
echo [2/6] Creating Container...
curl -X POST "%API_URL%/test-app" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"test-container\"}"
echo.
timeout /t 2 /nobreak >nul

echo.
echo [3/6] Creating MQTT Subscription (evt=1: creation)...
curl -X POST "%API_URL%/test-app/test-container/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"mqtt-sub\",\"evt\":1,\"endpoint\":\"mqtt://%MQTT_BROKER%:1883\"}"
echo.
timeout /t 2 /nobreak >nul

echo.
echo [4/6] Creating MQTT Subscription (evt=2: deletion)...
curl -X POST "%API_URL%/test-app/test-container/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"mqtt-delete-sub\",\"evt\":2,\"endpoint\":\"mqtt://%MQTT_BROKER%:1883\"}"
echo.
timeout /t 2 /nobreak >nul

echo.
echo ========================================
echo   NOW WATCH YOUR MQTT SUBSCRIBER!
echo ========================================
echo.
pause

echo.
echo [5/6] Creating Content-Instance (should trigger CREATION notification)...
curl -X POST "%API_URL%/test-app/test-container" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"data-1\",\"content_type\":\"application/json\",\"content\":\"{\\\"value\\\":42}\"}"
echo.
echo.
echo *** CHECK YOUR MQTT TERMINAL - DID YOU SEE A NOTIFICATION? ***
echo.
pause

echo.
echo [6/6] Deleting Content-Instance (should trigger DELETION notification)...
curl -X DELETE "%API_URL%/test-app/test-container/data-1" ^
  -H "Content-Type: application/json"
echo.
echo.
echo *** CHECK YOUR MQTT TERMINAL - DID YOU SEE A DELETE NOTIFICATION? ***
echo.
pause

echo.
echo ========================================
echo   CLEANUP
echo ========================================
echo.
curl -X DELETE "%API_URL%/test-app" -H "Content-Type: application/json"
echo.
echo.
echo Test complete!
echo.
echo EXPECTED RESULTS:
echo - You should have seen 2 notifications in mosquitto_sub
echo - One for content-instance creation (evt: creation)
echo - One for content-instance deletion (evt: deletion)
echo.
pause