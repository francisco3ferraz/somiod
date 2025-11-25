@echo off
REM ============================================================================
REM SOMIOD MIDDLEWARE - COMPLETE TESTING SUITE
REM Tests all CRUD + Discovery operations + Notification System
REM ============================================================================
REM 
REM USAGE:
REM 1. Update the BASE_URL variable below to match your server
REM 2. Ensure SOMIOD middleware is running
REM 3. Run: test_somiod_all.bat
REM
REM For MQTT testing, run mosquitto_sub in a separate terminal first:
REM   mosquitto_sub -h localhost -t "api/somiod/#" -v
REM   OR
REM   mosquitto_sub -h test.mosquitto.org -t "api/somiod/#" -v
REM ============================================================================

setlocal EnableDelayedExpansion

REM Configuration
set BASE_URL=https://localhost:44346/
set API_URL=%BASE_URL%/api/somiod

REM Colors for output (Windows 10+)
set "GREEN=[92m"
set "YELLOW=[93m"
set "RED=[91m"
set "BLUE=[94m"
set "RESET=[0m"

echo.
echo %BLUE%========================================%RESET%
echo %BLUE%   SOMIOD MIDDLEWARE TEST SUITE%RESET%
echo %BLUE%========================================%RESET%
echo.
echo Testing URL: %API_URL%
echo.
pause

REM ============================================================================
REM SECTION 1: APPLICATION CRUD + DISCOVERY
REM ============================================================================
echo.
echo %GREEN%[1/12] Testing APPLICATION operations...%RESET%
echo.

REM 1.1 CREATE Application
echo %YELLOW%1.1 POST - Create Application (warehouse-monitoring)%RESET%
curl -X POST "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"warehouse-monitoring\"}"
echo.
echo.
pause

REM 1.2 GET Application
echo %YELLOW%1.2 GET - Retrieve Application%RESET%
curl -X GET "%API_URL%/warehouse-monitoring" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

REM 1.3 UPDATE Application
echo %YELLOW%1.3 PUT - Update Application name%RESET%
curl -X PUT "%API_URL%/warehouse-monitoring" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"warehouse-monitoring-v2\"}"
echo.
echo.
pause

REM 1.4 DISCOVER Applications
echo %YELLOW%1.4 DISCOVERY - List all Applications%RESET%
curl -X GET "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: application"
echo.
echo.
pause

REM ============================================================================
REM SECTION 2: CONTAINER CRUD + DISCOVERY
REM ============================================================================
echo.
echo %GREEN%[2/12] Testing CONTAINER operations...%RESET%
echo.

REM 2.1 CREATE Container
echo %YELLOW%2.1 POST - Create Container (zone-a)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"zone-a\"}"
echo.
echo.
pause

echo %YELLOW%2.1b POST - Create Container (zone-b)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"zone-b\"}"
echo.
echo.
pause

REM 2.2 GET Container
echo %YELLOW%2.2 GET - Retrieve Container%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2/zone-a" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

REM 2.3 UPDATE Container
echo %YELLOW%2.3 PUT - Update Container name%RESET%
curl -X PUT "%API_URL%/warehouse-monitoring-v2/zone-a" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"zone-a-freezer\"}"
echo.
echo.
pause

REM 2.4 DISCOVER Containers
echo %YELLOW%2.4 DISCOVERY - List all Containers under Application%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: container"
echo.
echo.
pause

REM ============================================================================
REM SECTION 3: SUBSCRIPTION CRUD + DISCOVERY
REM ============================================================================
echo.
echo %GREEN%[3/12] Testing SUBSCRIPTION operations...%RESET%
echo.

REM 3.1 CREATE Subscription (MQTT)
echo %YELLOW%3.1 POST - Create MQTT Subscription (evt=1: creation)%RESET%
echo NOTE: Start mosquitto_sub first: mosquitto_sub -h localhost -t "api/somiod/#" -v
echo.
curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"mqtt-alert-sub\",\"evt\":1,\"endpoint\":\"mqtt://localhost:1883\"}"
echo.
echo.
pause

REM 3.1b CREATE Subscription (HTTP)
echo %YELLOW%3.1b POST - Create HTTP Subscription (evt=3: both)%RESET%
echo NOTE: Use webhook.site or similar to test HTTP notifications
echo.
curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"http-dashboard-sub\",\"evt\":3,\"endpoint\":\"https://webhook.site/your-unique-id\"}"
echo.
echo.
pause

REM 3.2 GET Subscription
echo %YELLOW%3.2 GET - Retrieve Subscription%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/subs/mqtt-alert-sub" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

REM 3.3 DISCOVER Subscriptions
echo %YELLOW%3.3 DISCOVERY - List all Subscriptions under Container%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2/zone-a-freezer" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: subscription"
echo.
echo.
pause

REM ============================================================================
REM SECTION 4: CONTENT-INSTANCE CRUD + DISCOVERY
REM ============================================================================
echo.
echo %GREEN%[4/12] Testing CONTENT-INSTANCE operations...%RESET%
echo.

REM 4.1 CREATE Content-Instance (TRIGGERS NOTIFICATIONS!)
echo %YELLOW%4.1 POST - Create Content-Instance (temp-reading-001)%RESET%
echo %RED%*** WATCH YOUR MQTT SUBSCRIBER - NOTIFICATION SHOULD APPEAR! ***%RESET%
echo.
curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"temp-reading-001\",\"content_type\":\"application/json\",\"content\":\"{\\\"temperature\\\":-18.5,\\\"unit\\\":\\\"celsius\\\",\\\"timestamp\\\":\\\"2025-11-25T14:30:00\\\"}\"}"
echo.
echo.
echo Check MQTT subscriber terminal for notification!
pause

echo %YELLOW%4.1b POST - Create another Content-Instance%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"temp-reading-002\",\"content_type\":\"application/json\",\"content\":\"{\\\"temperature\\\":-19.2,\\\"unit\\\":\\\"celsius\\\",\\\"timestamp\\\":\\\"2025-11-25T14:35:00\\\"}\"}"
echo.
echo.
pause

REM 4.2 GET Content-Instance
echo %YELLOW%4.2 GET - Retrieve Content-Instance%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/temp-reading-001" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

REM 4.3 DISCOVER Content-Instances
echo %YELLOW%4.3 DISCOVERY - List all Content-Instances under Container%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2/zone-a-freezer" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: content-instance"
echo.
echo.
pause

REM 4.4 DISCOVER Content-Instances (entire application)
echo %YELLOW%4.4 DISCOVERY - List all Content-Instances under Application%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: content-instance"
echo.
echo.
pause

REM ============================================================================
REM SECTION 5: TEST NOTIFICATION ON DELETE
REM ============================================================================
echo.
echo %GREEN%[5/12] Testing DELETE with Notifications...%RESET%
echo.

REM Create subscription for deletion events
echo %YELLOW%5.1 POST - Create Subscription for deletion events (evt=2)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"delete-alert-sub\",\"evt\":2,\"endpoint\":\"mqtt://localhost:1883\"}"
echo.
echo.
pause

REM DELETE Content-Instance (should trigger notification)
echo %YELLOW%5.2 DELETE - Delete Content-Instance%RESET%
echo %RED%*** WATCH YOUR MQTT SUBSCRIBER - DELETE NOTIFICATION SHOULD APPEAR! ***%RESET%
echo.
curl -X DELETE "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/temp-reading-002" ^
  -H "Content-Type: application/json"
echo.
echo.
echo Check MQTT subscriber for deletion notification!
pause

REM ============================================================================
REM SECTION 6: TEST CASCADE DELETES
REM ============================================================================
echo.
echo %GREEN%[6/12] Testing CASCADE DELETE operations...%RESET%
echo.

echo %YELLOW%6.1 DELETE - Delete Subscription%RESET%
curl -X DELETE "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/subs/mqtt-alert-sub" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

echo %YELLOW%6.2 DELETE - Delete Container (cascades to content-instances and subscriptions)%RESET%
curl -X DELETE "%API_URL%/warehouse-monitoring-v2/zone-b" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

REM ============================================================================
REM SECTION 7: ERROR HANDLING TESTS
REM ============================================================================
echo.
echo %GREEN%[7/12] Testing ERROR HANDLING...%RESET%
echo.

echo %YELLOW%7.1 GET - Non-existent Application (should return 404)%RESET%
curl -X GET "%API_URL%/non-existent-app" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

echo %YELLOW%7.2 POST - Duplicate Application (should return 409 Conflict)%RESET%
curl -X POST "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"warehouse-monitoring-v2\"}"
echo.
echo.
pause

echo %YELLOW%7.3 POST - Invalid resource name with spaces (should return 400)%RESET%
curl -X POST "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"invalid name with spaces\"}"
echo.
echo.
pause

echo %YELLOW%7.4 POST - Invalid subscription event type (should return 400)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"invalid-sub\",\"evt\":99,\"endpoint\":\"mqtt://localhost:1883\"}"
echo.
echo.
pause

REM ============================================================================
REM SECTION 8: AUTO-GENERATION TESTS
REM ============================================================================
echo.
echo %GREEN%[8/12] Testing AUTO-GENERATION of resource names...%RESET%
echo.

echo %YELLOW%8.1 POST - Create Application without resource_name (auto-generated)%RESET%
curl -X POST "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -d "{}"
echo.
echo.
pause

echo %YELLOW%8.2 POST - Create Container without resource_name (auto-generated)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -d "{}"
echo.
echo.
pause

REM ============================================================================
REM SECTION 9: COMPLEX DISCOVERY TESTS
REM ============================================================================
echo.
echo %GREEN%[9/12] Testing COMPLEX DISCOVERY scenarios...%RESET%
echo.

echo %YELLOW%9.1 DISCOVERY - All applications in entire SOMIOD%RESET%
curl -X GET "%API_URL%" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: application"
echo.
echo.
pause

echo %YELLOW%9.2 DISCOVERY - All content-instances across entire application%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: content-instance"
echo.
echo.
pause

echo %YELLOW%9.3 DISCOVERY - All subscriptions across entire application%RESET%
curl -X GET "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -H "somiod-discovery: subscription"
echo.
echo.
pause

REM ============================================================================
REM SECTION 10: NOTIFICATION STRESS TEST
REM ============================================================================
echo.
echo %GREEN%[10/12] Testing NOTIFICATION STRESS (multiple rapid creates)...%RESET%
echo.

echo %YELLOW%10.1 Creating 5 content-instances rapidly...%RESET%
echo %RED%*** WATCH MQTT - Should see 5 notifications! ***%RESET%
echo.

for /L %%i in (1,1,5) do (
    curl -X POST "%API_URL%/warehouse-monitoring-v2/zone-a-freezer" ^
      -H "Content-Type: application/json" ^
      -d "{\"resource_name\":\"stress-test-%%i\",\"content_type\":\"text/plain\",\"content\":\"Test data %%i\"}"
    echo.
    timeout /t 1 /nobreak >nul
)
echo.
pause

REM ============================================================================
REM SECTION 11: SUBSCRIPTION EVENT FILTERING TEST
REM ============================================================================
echo.
echo %GREEN%[11/12] Testing EVENT FILTERING (evt=1, evt=2, evt=3)...%RESET%
echo.

REM Create container for testing
curl -X POST "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"test-zone\"}"
echo.

echo %YELLOW%11.1 Create subscription for CREATION only (evt=1)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/test-zone/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"creation-only\",\"evt\":1,\"endpoint\":\"mqtt://localhost:1883\"}"
echo.
pause

echo %YELLOW%11.2 Create subscription for DELETION only (evt=2)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/test-zone/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"deletion-only\",\"evt\":2,\"endpoint\":\"mqtt://localhost:1883\"}"
echo.
pause

echo %YELLOW%11.3 Create subscription for BOTH events (evt=3)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/test-zone/subs" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"both-events\",\"evt\":3,\"endpoint\":\"mqtt://localhost:1883\"}"
echo.
pause

echo %YELLOW%11.4 CREATE content (should trigger evt=1 and evt=3 subscriptions)%RESET%
curl -X POST "%API_URL%/warehouse-monitoring-v2/test-zone" ^
  -H "Content-Type: application/json" ^
  -d "{\"resource_name\":\"filter-test-data\",\"content_type\":\"text/plain\",\"content\":\"Testing filtering\"}"
echo.
echo Check MQTT: Should see 2 notifications (creation-only + both-events)
pause

echo %YELLOW%11.5 DELETE content (should trigger evt=2 and evt=3 subscriptions)%RESET%
curl -X DELETE "%API_URL%/warehouse-monitoring-v2/test-zone/filter-test-data" ^
  -H "Content-Type: application/json"
echo.
echo Check MQTT: Should see 2 notifications (deletion-only + both-events)
pause

REM ============================================================================
REM SECTION 12: CLEANUP
REM ============================================================================
echo.
echo %GREEN%[12/12] CLEANUP - Deleting test resources...%RESET%
echo.

echo %YELLOW%12.1 DELETE - Application (cascades everything)%RESET%
curl -X DELETE "%API_URL%/warehouse-monitoring-v2" ^
  -H "Content-Type: application/json"
echo.
echo.
pause

echo.
echo %GREEN%========================================%RESET%
echo %GREEN%   ALL TESTS COMPLETED!%RESET%
echo %GREEN%========================================%RESET%
echo.
echo Summary of what was tested:
echo - All CRUD operations (Create, Read, Update, Delete)
echo - Discovery operations (applications, containers, content-instances, subscriptions)
echo - MQTT notifications on creation
echo - MQTT notifications on deletion  
echo - HTTP notifications (if webhook configured)
echo - Event type filtering (evt=1, evt=2, evt=3)
echo - Auto-generation of resource names
echo - Error handling (404, 409, 400)
echo - CASCADE deletes
echo - Stress testing (rapid notifications)
echo.
echo %YELLOW%IMPORTANT FOR REPORT:%RESET%
echo - Document all cURL commands used
echo - Include screenshots of MQTT notifications
echo - Show HTTP webhook notifications
echo - Demonstrate error responses
echo.
pause