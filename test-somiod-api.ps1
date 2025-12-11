# SOMIOD API Test Script
# This script tests all CRUD and Discovery operations for the SOMIOD middleware
# Usage: .\test-somiod-api.ps1

$baseUrl = "https://localhost:44346/api/somiod"

# Ignore SSL certificate errors (for development)
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

function Write-TestHeader {
    param([string]$title)
    Write-Host ""
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host " $title" -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
}

function Write-TestResult {
    param([string]$test, [bool]$success, [string]$details = "")
    if ($success) {
        Write-Host "[PASS] " -ForegroundColor Green -NoNewline
    } else {
        Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
    }
    Write-Host $test
    if ($details) {
        Write-Host "       $details" -ForegroundColor Gray
    }
}

function Invoke-ApiRequest {
    param(
        [string]$method,
        [string]$url,
        [hashtable]$headers = @{},
        [string]$body = $null
    )
    
    $headers["Content-Type"] = "application/json"
    
    try {
        $params = @{
            Method = $method
            Uri = $url
            Headers = $headers
        }
        
        if ($body) {
            $params["Body"] = $body
        }
        
        $response = Invoke-RestMethod @params
        return @{ Success = $true; Data = $response; StatusCode = 200 }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = $null
        $errorMessage = $_.Exception.Message
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd()
        } catch {}
        return @{ Success = $false; StatusCode = $statusCode; Error = $errorBody; Message = $errorMessage }
    }
}

# ============================================================================
# CLEANUP - Delete test data if exists
# ============================================================================
Write-TestHeader "CLEANUP - Removing existing test data"

$cleanup = Invoke-ApiRequest -method "DELETE" -url "$baseUrl/test-app"
Write-Host "Cleanup completed (test-app removed if existed)" -ForegroundColor Gray

# ============================================================================
# APPLICATION TESTS
# ============================================================================
Write-TestHeader "APPLICATION TESTS"

# Test 1: Create Application
Write-Host "`n--- POST /api/somiod (Create Application) ---" -ForegroundColor Yellow
$createAppBody = '{"resource_name": "test-app"}'
$result = Invoke-ApiRequest -method "POST" -url $baseUrl -body $createAppBody
Write-TestResult "Create Application 'test-app'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 2: Create Application with auto-generated name
Write-Host "`n--- POST /api/somiod (Create Application - Auto Name) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url $baseUrl -body '{}'
Write-TestResult "Create Application with auto-generated name" $result.Success
$autoAppName = $null
if ($result.Success) {
    $autoAppName = $result.Data.resource_name
    Write-Host "       Auto-generated name: $autoAppName" -ForegroundColor Gray
}

# Test 3: Create Duplicate Application (should fail with 409)
Write-Host "`n--- POST /api/somiod (Duplicate - Should Fail) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url $baseUrl -body $createAppBody
Write-TestResult "Reject duplicate application (409 Conflict)" (-not $result.Success -and $result.StatusCode -eq 409)

# Test 4: Get Application
Write-Host "`n--- GET /api/somiod/test-app (Get Application) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app"
Write-TestResult "Get Application 'test-app'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 5: Update Application
Write-Host "`n--- PUT /api/somiod/test-app (Update Application) ---" -ForegroundColor Yellow
$updateAppBody = '{"resource_name": "test-app-updated"}'
$result = Invoke-ApiRequest -method "PUT" -url "$baseUrl/test-app" -body $updateAppBody
Write-TestResult "Update Application name to 'test-app-updated'" $result.Success

# Rename back for further tests
$result = Invoke-ApiRequest -method "PUT" -url "$baseUrl/test-app-updated" -body '{"resource_name": "test-app"}'

# Test 6: Discover All Applications
Write-Host "`n--- GET /api/somiod (Discover Applications) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "application" }
$result = Invoke-ApiRequest -method "GET" -url $baseUrl -headers $headers
Write-TestResult "Discover all applications" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) application(s)" -ForegroundColor Gray
    $result.Data | ForEach-Object { Write-Host "       - $_" -ForegroundColor Gray }
}

# ============================================================================
# CONTAINER TESTS
# ============================================================================
Write-TestHeader "CONTAINER TESTS"

# Test 7: Create Container
Write-Host "`n--- POST /api/somiod/test-app (Create Container) ---" -ForegroundColor Yellow
$createContainerBody = '{"resource_name": "test-container"}'
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app" -body $createContainerBody
Write-TestResult "Create Container 'test-container'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 8: Create second container
Write-Host "`n--- POST /api/somiod/test-app (Create Second Container) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app" -body '{"resource_name": "test-container-2"}'
Write-TestResult "Create Container 'test-container-2'" $result.Success

# Test 9: Get Container
Write-Host "`n--- GET /api/somiod/test-app/test-container (Get Container) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app/test-container"
Write-TestResult "Get Container 'test-container'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 10: Update Container
Write-Host "`n--- PUT /api/somiod/test-app/test-container (Update Container) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "PUT" -url "$baseUrl/test-app/test-container" -body '{"resource_name": "test-container-renamed"}'
Write-TestResult "Update Container name" $result.Success
# Rename back
$result = Invoke-ApiRequest -method "PUT" -url "$baseUrl/test-app/test-container-renamed" -body '{"resource_name": "test-container"}'

# Test 11: Discover Containers under Application
Write-Host "`n--- GET /api/somiod/test-app (Discover Containers) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "container" }
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app" -headers $headers
Write-TestResult "Discover containers under 'test-app'" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) container(s)" -ForegroundColor Gray
    $result.Data | ForEach-Object { Write-Host "       - $_" -ForegroundColor Gray }
}

# Test 12: Global Container Discovery
Write-Host "`n--- GET /api/somiod (Global Container Discovery) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "container" }
$result = Invoke-ApiRequest -method "GET" -url $baseUrl -headers $headers
Write-TestResult "Global container discovery" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) container(s) globally" -ForegroundColor Gray
}

# ============================================================================
# CONTENT-INSTANCE TESTS
# ============================================================================
Write-TestHeader "CONTENT-INSTANCE TESTS"

# Test 13: Create Content-Instance
Write-Host "`n--- POST /api/somiod/test-app/test-container (Create Content-Instance) ---" -ForegroundColor Yellow
$createContentBody = '{"resource_name": "test-data", "content_type": "application/json", "content": "{\"temperature\": 23.5}"}'
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container" -body $createContentBody
Write-TestResult "Create Content-Instance 'test-data'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 14: Create second content-instance (text/plain)
Write-Host "`n--- POST /api/somiod/test-app/test-container (Create Second Content-Instance) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container" -body '{"resource_name": "test-message", "content_type": "text/plain", "content": "Hello World"}'
Write-TestResult "Create Content-Instance 'test-message' (text/plain)" $result.Success

# Test 15: Get Content-Instance
Write-Host "`n--- GET /api/somiod/test-app/test-container/test-data (Get Content-Instance) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app/test-container/test-data"
Write-TestResult "Get Content-Instance 'test-data'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 16: Discover Content-Instances under Container
Write-Host "`n--- GET /api/somiod/test-app/test-container (Discover Content-Instances) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "content-instance" }
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app/test-container" -headers $headers
Write-TestResult "Discover content-instances under container" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) content-instance(s)" -ForegroundColor Gray
    $result.Data | ForEach-Object { Write-Host "       - $_" -ForegroundColor Gray }
}

# Test 17: Global Content-Instance Discovery
Write-Host "`n--- GET /api/somiod (Global Content-Instance Discovery) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "content-instance" }
$result = Invoke-ApiRequest -method "GET" -url $baseUrl -headers $headers
Write-TestResult "Global content-instance discovery" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) content-instance(s) globally" -ForegroundColor Gray
}

# ============================================================================
# SUBSCRIPTION TESTS
# ============================================================================
Write-TestHeader "SUBSCRIPTION TESTS"

# Test 18: Create MQTT Subscription (Creation Events)
Write-Host "`n--- POST /api/somiod/test-app/test-container/subs (Create MQTT Subscription) ---" -ForegroundColor Yellow
$createSubBody = '{"resource_name": "mqtt-create-listener", "evt": 1, "endpoint": "mqtt://localhost:1883"}'
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container/subs" -body $createSubBody
Write-TestResult "Create MQTT Subscription (evt=1 creation)" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 19: Create MQTT Subscription (Deletion Events)
Write-Host "`n--- POST /api/somiod/test-app/test-container/subs (Create Delete Subscription) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container/subs" -body '{"resource_name": "mqtt-delete-listener", "evt": 2, "endpoint": "mqtt://localhost:1883"}'
Write-TestResult "Create MQTT Subscription (evt=2 deletion)" $result.Success

# Test 20: Create HTTP Subscription
Write-Host "`n--- POST /api/somiod/test-app/test-container/subs (Create HTTP Subscription) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container/subs" -body '{"resource_name": "http-webhook", "evt": 1, "endpoint": "http://localhost:8080/webhook"}'
Write-TestResult "Create HTTP Subscription" $result.Success

# Test 21: Get Subscription
Write-Host "`n--- GET /api/somiod/test-app/test-container/subs/mqtt-create-listener (Get Subscription) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app/test-container/subs/mqtt-create-listener"
Write-TestResult "Get Subscription 'mqtt-create-listener'" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Test 22: Discover Subscriptions under Container
Write-Host "`n--- GET /api/somiod/test-app/test-container (Discover Subscriptions) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "subscription" }
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/test-app/test-container" -headers $headers
Write-TestResult "Discover subscriptions under container" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) subscription(s)" -ForegroundColor Gray
    $result.Data | ForEach-Object { Write-Host "       - $_" -ForegroundColor Gray }
}

# Test 23: Global Subscription Discovery
Write-Host "`n--- GET /api/somiod (Global Subscription Discovery) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "subscription" }
$result = Invoke-ApiRequest -method "GET" -url $baseUrl -headers $headers
Write-TestResult "Global subscription discovery" $result.Success
if ($result.Success) {
    Write-Host "       Found: $($result.Data.Count) subscription(s) globally" -ForegroundColor Gray
}

# ============================================================================
# NOTIFICATION TRIGGER TEST
# ============================================================================
Write-TestHeader "NOTIFICATION TRIGGER TEST"

Write-Host "`nNote: To fully test notifications, run an MQTT subscriber:" -ForegroundColor Yellow
Write-Host "      mosquitto_sub -h localhost -t 'api/somiod/test-app/test-container'" -ForegroundColor Gray

# Test 24: Create content-instance to trigger notification
Write-Host "`n--- POST (Create Content-Instance to Trigger Creation Notification) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container" -body '{"resource_name": "notification-test", "content_type": "text/plain", "content": "This should trigger a notification"}'
Write-TestResult "Create content-instance (triggers evt=1 notification)" $result.Success
if (-not $result.Success) {
    Write-Host "       Status Code: $($result.StatusCode)" -ForegroundColor Red
    Write-Host "       Error: $($result.Message)" -ForegroundColor Red
    if ($result.Error) {
        Write-Host "       Body: $($result.Error)" -ForegroundColor Red
    }
}
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# ============================================================================
# DELETE TESTS
# ============================================================================
Write-TestHeader "DELETE TESTS"

# Test 25: Delete Subscription
Write-Host "`n--- DELETE /api/somiod/test-app/test-container/subs/http-webhook ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "DELETE" -url "$baseUrl/test-app/test-container/subs/http-webhook"
Write-TestResult "Delete Subscription 'http-webhook'" $result.Success

# Test 26: Delete Content-Instance (should trigger deletion notification)
Write-Host "`n--- DELETE /api/somiod/test-app/test-container/notification-test ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "DELETE" -url "$baseUrl/test-app/test-container/notification-test"
Write-TestResult "Delete Content-Instance 'notification-test' (triggers evt=2 notification)" $result.Success

# Test 27: Delete Container (CASCADE)
Write-Host "`n--- DELETE /api/somiod/test-app/test-container-2 (Delete Container CASCADE) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "DELETE" -url "$baseUrl/test-app/test-container-2"
Write-TestResult "Delete Container 'test-container-2' (CASCADE)" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# ============================================================================
# ERROR HANDLING TESTS
# ============================================================================
Write-TestHeader "ERROR HANDLING TESTS"

# Test 28: Get non-existent application
Write-Host "`n--- GET /api/somiod/non-existent (404 Test) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "GET" -url "$baseUrl/non-existent"
Write-TestResult "Return 404 for non-existent application" (-not $result.Success -and $result.StatusCode -eq 404)

# Test 29: Invalid discovery header
Write-Host "`n--- GET /api/somiod (Invalid Discovery Header) ---" -ForegroundColor Yellow
$headers = @{ "somiod-discovery" = "invalid-type" }
$result = Invoke-ApiRequest -method "GET" -url $baseUrl -headers $headers
Write-TestResult "Return 400 for invalid discovery type" (-not $result.Success -and $result.StatusCode -eq 400)

# Test 30: Missing discovery header
Write-Host "`n--- GET /api/somiod (Missing Discovery Header) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "GET" -url $baseUrl
Write-TestResult "Return 400 when discovery header is missing" (-not $result.Success -and $result.StatusCode -eq 400)

# Test 31: Invalid resource name
Write-Host "`n--- POST (Invalid Resource Name) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url $baseUrl -body '{"resource_name": "invalid name with spaces!"}'
Write-TestResult "Return 400 for invalid resource name" (-not $result.Success -and $result.StatusCode -eq 400)

# Test 32: Invalid event type for subscription
Write-Host "`n--- POST (Invalid Event Type) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "POST" -url "$baseUrl/test-app/test-container/subs" -body '{"resource_name": "bad-sub", "evt": 99, "endpoint": "mqtt://localhost:1883"}'
Write-TestResult "Return 400 for invalid event type" (-not $result.Success -and $result.StatusCode -eq 400)

# ============================================================================
# FINAL CLEANUP
# ============================================================================
Write-TestHeader "FINAL CLEANUP"

# Delete test application (CASCADE deletes everything)
Write-Host "`n--- DELETE /api/somiod/test-app (Final Cleanup - CASCADE) ---" -ForegroundColor Yellow
$result = Invoke-ApiRequest -method "DELETE" -url "$baseUrl/test-app"
Write-TestResult "Delete Application 'test-app' (CASCADE all children)" $result.Success
if ($result.Success) {
    Write-Host "       Response: $($result.Data | ConvertTo-Json -Compress)" -ForegroundColor Gray
}

# Delete auto-generated app if exists
if ($autoAppName) {
    $result = Invoke-ApiRequest -method "DELETE" -url "$baseUrl/$autoAppName"
    Write-Host "       Cleaned up auto-generated app: $autoAppName" -ForegroundColor Gray
}

# ============================================================================
# SUMMARY
# ============================================================================
Write-TestHeader "TEST COMPLETE"
Write-Host ""
Write-Host "All CRUD and Discovery operations have been tested!" -ForegroundColor Green
Write-Host ""
Write-Host "Tested Operations:" -ForegroundColor White
Write-Host "  - Application: CREATE, READ, UPDATE, DELETE, DISCOVER" -ForegroundColor Gray
Write-Host "  - Container: CREATE, READ, UPDATE, DELETE, DISCOVER" -ForegroundColor Gray
Write-Host "  - Content-Instance: CREATE, READ, DELETE, DISCOVER" -ForegroundColor Gray
Write-Host "  - Subscription: CREATE, READ, DELETE, DISCOVER" -ForegroundColor Gray
Write-Host "  - Global Discovery: application, container, content-instance, subscription" -ForegroundColor Gray
Write-Host "  - Notifications: Creation and Deletion triggers" -ForegroundColor Gray
Write-Host "  - Error Handling: 400, 404, 409 responses" -ForegroundColor Gray
Write-Host ""
