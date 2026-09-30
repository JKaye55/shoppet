param(
    [string]$BaseUrl = "http://localhost:5020/api",
    [switch]$IncludeCheckout
)

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0
$script:Results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param([string]$Name, [bool]$Success, [string]$Details = "")
    if ($Success) { $script:Passed++ } else { $script:Failed++ }
    $script:Results.Add([pscustomobject]@{
        Test = $Name
        Result = if ($Success) { "PASS" } else { "FAIL" }
        Details = $Details
    })
    $mark = if ($Success) { "[PASS]" } else { "[FAIL]" }
    Write-Host "$mark $Name $Details"
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body = $null
    )

    $uri = "$BaseUrl/$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json"
    }

    $json = $Body | ConvertTo-Json -Depth 10
    return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body $json
}

function Test-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    try {
        $value = & $Action
        Add-Result -Name $Name -Success $true
        return $value
    }
    catch {
        Add-Result -Name $Name -Success $false -Details $_.Exception.Message
        return $null
    }
}

$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$primaryEmail = "smoke.$stamp.1@shoppet.local"
$secondaryEmail = "smoke.$stamp.2@shoppet.local"
$password = "SmokeTest123!"

Write-Host ""
Write-Host "ShoppetCare API smoke test"
Write-Host "Base URL: $BaseUrl"
Write-Host "Checkout test: $($IncludeCheckout.IsPresent)"
Write-Host ""

# ---------------------------------------------------------------------------
# AUTH + PROFILE
# ---------------------------------------------------------------------------
$primary = Test-Step "Auth - register primary user" {
    Invoke-Api POST "auth/register" @{
        fullName = "Smoke Primary $stamp"
        email = $primaryEmail
        password = $password
    }
}

$secondary = Test-Step "Auth - register secondary user" {
    Invoke-Api POST "auth/register" @{
        fullName = "Smoke Secondary $stamp"
        email = $secondaryEmail
        password = $password
    }
}

if ($primary) {
    Test-Step "Auth - login primary user" {
        $login = Invoke-Api POST "auth/login" @{
            email = $primaryEmail
            password = $password
        }
        if ($login.userId -ne $primary.userId) { throw "Login returned wrong user ID." }
        $login
    } | Out-Null

    Test-Step "Profile - read" {
        Invoke-Api GET "profile/$($primary.userId)"
    } | Out-Null

    Test-Step "Profile - update" {
        Invoke-Api PUT "profile/update" @{
            userId = $primary.userId
            fullName = "Smoke Primary Updated $stamp"
            profilePictureBase64 = ""
        }
    } | Out-Null
}

# ---------------------------------------------------------------------------
# PET CRUD
# ---------------------------------------------------------------------------
$pet = $null
if ($primary) {
    $pet = Test-Step "Pets - create" {
        Invoke-Api POST "pets" @{
            userId = $primary.userId
            name = "SmokePet-$stamp"
            species = "Dog"
            breed = "Aspin"
            ageYears = 3
            weight = "12 kg"
            photoUrl = ""
        }
    }

    Test-Step "Pets - read list" {
        $items = @(Invoke-Api GET "pets?userId=$($primary.userId)")
        if (-not ($items | Where-Object { $_.id -eq $pet.id })) { throw "Created pet not returned." }
        $items
    } | Out-Null

    if ($pet) {
        Test-Step "Pets - update" {
            Invoke-Api PUT "pets/$($pet.id)" @{
                userId = $primary.userId
                name = "SmokePet-Updated-$stamp"
                species = "Dog"
                breed = "Mixed"
                ageYears = 4
                weight = "13 kg"
                photoUrl = ""
            }
        } | Out-Null
    }
}

# ---------------------------------------------------------------------------
# CONTACT CRUD
# ---------------------------------------------------------------------------
$contact = $null
if ($primary) {
    $contact = Test-Step "Contacts - create" {
        Invoke-Api POST "contacts" @{
            userId = $primary.userId
            name = "Smoke Vet"
            role = "Veterinarian"
            address = "Lipa City"
            phone = "09170000000"
            isEmergency = $true
        }
    }

    Test-Step "Contacts - read list" {
        $items = @(Invoke-Api GET "contacts?userId=$($primary.userId)")
        if (-not ($items | Where-Object { $_.id -eq $contact.id })) { throw "Created contact not returned." }
        $items
    } | Out-Null

    if ($contact) {
        Test-Step "Contacts - update" {
            Invoke-Api PUT "contacts/$($contact.id)" @{
                userId = $primary.userId
                name = "Smoke Vet Updated"
                role = "Clinic"
                address = "Lipa City, Batangas"
                phone = "09171111111"
                isEmergency = $false
            }
        } | Out-Null
    }
}

# ---------------------------------------------------------------------------
# HEALTH + FOOD CRUD
# ---------------------------------------------------------------------------
$health = $null
$food = $null
if ($pet) {
    $health = Test-Step "Health - create" {
        Invoke-Api POST "pets/$($pet.id)/healthlogs" @{
            type = "vaccine"
            name = "Smoke Vaccine"
            dueDate = "2026/12/01, 08:00"
            completed = $false
            dateAdministered = "2026/09/30, 00:00"
            validityInterval = 12
            validityUnit = "Months"
            medicationIntervalHours = 0
            timeStarted = ""
            dosageTotal = 0
            dosageRemaining = 0
            checkupDate = ""
            documentPaths = "smoke-a.pdf;smoke-b.pdf"
        }
    }

    Test-Step "Health - read list" {
        $items = @(Invoke-Api GET "pets/$($pet.id)/healthlogs")
        if (-not ($items | Where-Object { $_.id -eq $health.id })) { throw "Created health log not returned." }
        $items
    } | Out-Null

    if ($health) {
        Test-Step "Health - update" {
            Invoke-Api PUT "pets/$($pet.id)/healthlogs/$($health.id)" @{
                type = "vaccine"
                name = "Smoke Vaccine Updated"
                dueDate = "2027/12/01, 08:00"
                completed = $false
                dateAdministered = "2026/09/30, 00:00"
                validityInterval = 12
                validityUnit = "Months"
                medicationIntervalHours = 0
                timeStarted = ""
                dosageTotal = 0
                dosageRemaining = 0
                checkupDate = ""
                documentPaths = "smoke-a.pdf;smoke-b.pdf"
            }
        } | Out-Null

        Test-Step "Health - complete" {
            Invoke-Api PUT "pets/$($pet.id)/healthlogs/$($health.id)/complete" @{
                nextDueDate = "2027/12/01, 08:00"
            }
        } | Out-Null
    }

    $food = Test-Step "Food - create" {
        Invoke-Api POST "pets/$($pet.id)/foodlogs" @{
            foodName = "Smoke Food"
            amountGrams = 120
            intervalHours = 8
            intervalMinutes = 30
            startTimestamp = "2026/09/30, 08:00:00"
            lastFedTimestamp = ""
            fedDate = "2026/09/30, 08:00:00"
            notes = "Smoke test"
        }
    }

    Test-Step "Food - read list" {
        $items = @(Invoke-Api GET "pets/$($pet.id)/foodlogs")
        if (-not ($items | Where-Object { $_.id -eq $food.id })) { throw "Created food log not returned." }
        $items
    } | Out-Null

    if ($food) {
        Test-Step "Food - update" {
            Invoke-Api PUT "pets/$($pet.id)/foodlogs/$($food.id)" @{
                foodName = "Smoke Food Updated"
                amountGrams = 140
                intervalHours = 7
                intervalMinutes = 15
                startTimestamp = "2026/09/30, 09:00:00"
                lastFedTimestamp = ""
                fedDate = "2026/09/30, 09:00:00"
                notes = "Smoke update"
            }
        } | Out-Null

        Test-Step "Food - complete" {
            Invoke-Api PUT "pets/$($pet.id)/foodlogs/$($food.id)/complete"
        } | Out-Null
    }
}

# ---------------------------------------------------------------------------
# COMMUNITY
# ---------------------------------------------------------------------------
$post = $null
$commentId = $null
if ($primary) {
    $post = Test-Step "Community - create post" {
        Invoke-Api POST "community" @{
            userId = $primary.userId
            petId = if ($pet) { $pet.id } else { $null }
            authorName = "Smoke Primary"
            petName = if ($pet) { "SmokePet" } else { "" }
            content = "Smoke post $stamp"
            imageUrls = $null
        }
    }

    Test-Step "Community - read posts" {
        $items = @(Invoke-Api GET "community?userId=$($primary.userId)")
        if (-not ($items | Where-Object { $_.id -eq $post.id })) { throw "Created post not returned." }
        $items
    } | Out-Null

    if ($post) {
        Test-Step "Community - like post" {
            Invoke-Api POST "community/$($post.id)/like" @{ userId = $primary.userId }
        } | Out-Null

        Test-Step "Community - edit post" {
            Invoke-Api PUT "community/$($post.id)" @{
                content = "Smoke post updated $stamp"
                imageUrls = ""
                petId = if ($pet) { $pet.id } else { $null }
                petName = if ($pet) { "SmokePet-Updated" } else { "" }
            }
        } | Out-Null

        $commentText = "Smoke comment $stamp"
        Test-Step "Community - create comment" {
            Invoke-Api POST "community/$($post.id)/comments" @{
                userId = $primary.userId
                parentCommentId = $null
                content = $commentText
            }
        } | Out-Null

        Test-Step "Community - read comments" {
            $items = @(Invoke-Api GET "community/$($post.id)/comments?userId=$($primary.userId)")
            $found = $items | Where-Object { $_.content -eq $commentText } | Select-Object -First 1
            if (-not $found) { throw "Created comment not returned." }
            $script:commentId = $found.id
            $items
        } | Out-Null

        if ($script:commentId) {
            Test-Step "Community - like comment" {
                Invoke-Api POST "community/comments/$($script:commentId)/like" @{
                    userId = $primary.userId
                }
            } | Out-Null

            Test-Step "Community - delete comment" {
                Invoke-Api DELETE "community/comments/$($script:commentId)"
            } | Out-Null
        }
    }
}

# ---------------------------------------------------------------------------
# MARKETPLACE
# ---------------------------------------------------------------------------
$listing = $null
if ($primary) {
    $listing = Test-Step "Marketplace - create listing" {
        Invoke-Api POST "marketplace" @{
            userId = $primary.userId
            sellerName = "Smoke Primary"
            title = "Smoke Listing $stamp"
            description = "Automated smoke test"
            price = 99.50
            category = "Accessories"
            condition = "Like New"
            location = "Lipa City"
            imageUrls = $null
        }
    }

    Test-Step "Marketplace - read own listings" {
        $items = @(Invoke-Api GET "marketplace/my/$($primary.userId)")
        if (-not ($items | Where-Object { $_.id -eq $listing.id })) { throw "Created listing not returned." }
        $items
    } | Out-Null

    if ($listing) {
        Test-Step "Marketplace - update listing" {
            Invoke-Api PUT "marketplace/$($listing.id)" @{
                userId = $primary.userId
                title = "Smoke Listing Updated $stamp"
                description = "Updated automated smoke test"
                price = 109.50
                category = "Accessories"
                condition = "Good"
                location = "Lipa City"
                imageUrls = ""
            }
        } | Out-Null
    }
}

# ---------------------------------------------------------------------------
# MOCK PAYMENT
# ---------------------------------------------------------------------------
if ($primary -and $listing) {
    Test-Step "Mock Payment - success" {
        $payment = Invoke-Api POST "payments/mock" @{
            userId = $primary.userId
            listingId = $listing.id
            amount = 109.50
            paymentMethod = "GCash Mock"
            type = "MarketplacePurchase"
            simulateSuccess = $true
        }
        if ($payment.status -ne "SimulatedPaid") { throw "Expected SimulatedPaid status." }
        $payment
    } | Out-Null
}

# ---------------------------------------------------------------------------
# MESSAGING
# ---------------------------------------------------------------------------
if ($primary -and $secondary) {
    Test-Step "Messages - send" {
        Invoke-Api POST "messages" @{
            senderId = $primary.userId
            receiverId = $secondary.userId
            listingId = if ($listing) { $listing.id } else { $null }
            text = "Smoke message $stamp"
        }
    } | Out-Null

    Test-Step "Messages - conversations" {
        $items = @(Invoke-Api GET "messages/$($primary.userId)")
        if (-not ($items | Where-Object { $_.contactId -eq $secondary.userId })) {
            throw "Conversation not returned."
        }
        $items
    } | Out-Null

    Test-Step "Messages - chat history" {
        $items = @(Invoke-Api GET "messages/chat/$($primary.userId)/$($secondary.userId)")
        if (-not ($items | Where-Object { $_.text -eq "Smoke message $stamp" })) {
            throw "Sent message not returned."
        }
        $items
    } | Out-Null

    Test-Step "Messages - unread count" {
        Invoke-Api GET "messages/unreadCount/$($secondary.userId)"
    } | Out-Null

    Test-Step "Messages - reset unread" {
        Invoke-Api POST "messages/resetUnread/$($secondary.userId)/$($primary.userId)"
    } | Out-Null
}

# ---------------------------------------------------------------------------
# SHOP + CART + OPTIONAL CHECKOUT
# ---------------------------------------------------------------------------
$categories = Test-Step "Shop - categories" {
    @(Invoke-Api GET "shop/categories")
}

$products = Test-Step "Shop - products" {
    @(Invoke-Api GET "shop/products")
}

$cartProduct = $null
if ($products) {
    $cartProduct = $products |
        Where-Object { $_.isAvailable -eq $true -and $_.stockQuantity -gt 0 } |
        Select-Object -First 1
}

if ($primary -and $cartProduct) {
    $cart = Test-Step "Cart - add item" {
        Invoke-Api POST "cart/items" @{
            userId = $primary.userId
            productId = $cartProduct.id
            quantity = 1
        }
    }

    Test-Step "Cart - read" {
        Invoke-Api GET "cart?userId=$($primary.userId)"
    } | Out-Null

    $cartItem = $null
    if ($cart) {
        $cartItem = @($cart.items) |
            Where-Object { $_.productId -eq $cartProduct.id } |
            Select-Object -First 1
    }

    if ($cartItem) {
        Test-Step "Cart - update quantity" {
            Invoke-Api PUT "cart/items/$($cartItem.id)?userId=$($primary.userId)" @{
                quantity = 1
            }
        } | Out-Null

        Test-Step "Cart - remove item" {
            Invoke-Api DELETE "cart/items/$($cartItem.id)?userId=$($primary.userId)"
        } | Out-Null
    }

    Test-Step "Cart - add before clear" {
        Invoke-Api POST "cart/items" @{
            userId = $primary.userId
            productId = $cartProduct.id
            quantity = 1
        }
    } | Out-Null

    Test-Step "Cart - clear" {
        Invoke-Api DELETE "cart?userId=$($primary.userId)"
    } | Out-Null

    if ($IncludeCheckout) {
        Test-Step "Cart - add before checkout" {
            Invoke-Api POST "cart/items" @{
                userId = $primary.userId
                productId = $cartProduct.id
                quantity = 1
            }
        } | Out-Null

        Test-Step "Checkout - create order" {
            Invoke-Api POST "cart/checkout?userId=$($primary.userId)"
        } | Out-Null

        Test-Step "Orders - read history" {
            $items = @(Invoke-Api GET "cart/orders?userId=$($primary.userId)")
            if ($items.Count -eq 0) { throw "No order returned after checkout." }
            $items
        } | Out-Null
    }
}
elseif (-not $cartProduct) {
    Add-Result "Cart - server CRUD" $true "SKIPPED: no in-stock product exists in the database."
}

# ---------------------------------------------------------------------------
# CLEANUP
# ---------------------------------------------------------------------------
if ($listing) {
    Test-Step "Marketplace - delete listing" {
        Invoke-Api DELETE "marketplace/$($listing.id)?userId=$($primary.userId)"
    } | Out-Null
}

if ($post) {
    Test-Step "Community - delete post" {
        Invoke-Api DELETE "community/$($post.id)"
    } | Out-Null
}

if ($food) {
    Test-Step "Food - delete" {
        Invoke-Api DELETE "pets/$($pet.id)/foodlogs/$($food.id)"
    } | Out-Null
}

if ($health) {
    Test-Step "Health - delete" {
        Invoke-Api DELETE "pets/$($pet.id)/healthlogs/$($health.id)"
    } | Out-Null
}

if ($contact) {
    Test-Step "Contacts - delete" {
        Invoke-Api DELETE "contacts/$($contact.id)"
    } | Out-Null
}

if ($pet) {
    Test-Step "Pets - delete" {
        Invoke-Api DELETE "pets/$($pet.id)"
    } | Out-Null
}

Write-Host ""
Write-Host "================ ShoppetCare Smoke Test Summary ================"
$script:Results | Format-Table -AutoSize
Write-Host "Passed: $script:Passed"
Write-Host "Failed: $script:Failed"
Write-Host ""
Write-Host "Note: temporary smoke-test user accounts are intentionally left in UserAccounts"
Write-Host "because the current product has no user-delete API."
if ($IncludeCheckout) {
    Write-Host "Checkout testing creates one SQL Server test order and decrements one seeded test product stock."
}
Write-Host ""

if ($script:Failed -gt 0) {
    exit 1
}

exit 0
