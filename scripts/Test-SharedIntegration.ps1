param([string]$ApiBaseUrl='http://localhost:5020/api')
$ErrorActionPreference='Stop'
$ApiBaseUrl=$ApiBaseUrl.TrimEnd('/')
function CallApi($method,$path,$token,$body) {
    $args=@{Method=$method;Uri="$ApiBaseUrl/$path"}
    if($token){$args.Headers=@{Authorization="Bearer $token"}}
    if($null -ne $body){$args.ContentType='application/json';$args.Body=($body|ConvertTo-Json -Depth 10)}
    Invoke-RestMethod @args
}
function Assert($condition,$label){if(-not $condition){throw "FAIL: $label"};Write-Host "PASS: $label" -ForegroundColor Green}
function ExpectFailure($code,[scriptblock]$action,$label){$failed=$false;try{& $action|Out-Null}catch{if([int]$_.Exception.Response.StatusCode -ne $code){throw};$failed=$true};Assert $failed $label}
$id=[guid]::NewGuid().ToString('N').Substring(0,10)
$password='A happy ShoppetCare demo passphrase'
$seller=CallApi POST 'auth/register' $null @{FullName="Demo seller $id";Email="seller-$id@example.test";Password=$password}
$buyer=CallApi POST 'auth/register' $null @{FullName="Demo buyer $id";Email="buyer-$id@example.test";Password=$password}
Assert ($seller.Token.Length -eq 64 -and $buyer.Token.Length -eq 64) 'Random server sessions issued'
ExpectFailure 401 {CallApi GET 'pets' $null $null} 'Anonymous pet access rejected'
ExpectFailure 403 {CallApi GET "profile/$($seller.UserId)" $buyer.Token $null} 'Another owner profile is private'
$pet=CallApi POST 'pets' $buyer.Token @{UserId=$buyer.UserId;Name="Demo pet $id";Species='Dog';Breed='Mixed';AgeYears=2;Weight='8 kg'}
Assert ($pet.Id -gt 0 -and $pet.CardId) 'Shared pet and digital ID created'
ExpectFailure 400 {CallApi POST 'pets' $buyer.Token @{UserId=$buyer.UserId;Name='Second free pet';Species='Dog'}} 'Free account limited to one pet'
$health=CallApi POST "pets/$($pet.Id)/healthlogs" $buyer.Token @{Type='medication';Name='Demo medicine';Notes='Shared care note';VetName='Demo veterinarian';RecordDate=(Get-Date).ToString('o');DueDate=(Get-Date).AddHours(8).ToString('o');DosageTotal=3;DosageRemaining=2;MedicationIntervalHours=8}
$healthRows=@(CallApi GET "pets/$($pet.Id)/healthlogs" $buyer.Token $null)
Assert ($healthRows[0].DosageRemaining -eq 2 -and $healthRows[0].Notes -eq 'Shared care note') 'Care notes and dose progress saved'
ExpectFailure 403 {CallApi GET "pets/$($pet.Id)/healthlogs" $seller.Token $null} 'Another owner health records denied'
$food=CallApi POST "pets/$($pet.Id)/foodlogs" $buyer.Token @{FoodName='Demo food';AmountGrams=100;IntervalHours=8;IntervalMinutes=0;StartTimestamp=(Get-Date).ToString('o');Notes='Shared feeding routine'}
CallApi POST "pets/$($pet.Id)/foodlogs/$($food.Id)/done" $buyer.Token $null|Out-Null
$visit=CallApi POST 'vetvisits' $buyer.Token @{UserId=$buyer.UserId;PetId=$pet.Id;ClinicName='Personal vet';VisitAt=(Get-Date).AddDays(1).ToString('o');Purpose='Checkup';Notes='Owner reminder'}
Assert (@(CallApi GET "vetvisits?userId=$($buyer.UserId)" $buyer.Token $null).Count -eq 1) 'Personal vet reminder saved'
$post=CallApi POST 'community' $buyer.Token @{UserId=$buyer.UserId;PetId=$pet.Id;Caption="Demo story $id";ImageUrls=''}
$comment=CallApi POST "community/$($post.Id)/comments" $seller.Token @{UserId=$seller.UserId;Content='A lovely pet!'}
CallApi POST "community/$($post.Id)/comments" $buyer.Token @{UserId=$buyer.UserId;ParentCommentId=$comment.Id;Content='Thank you!'}|Out-Null
CallApi POST "community/comments/$($comment.Id)/like" $buyer.Token @{UserId=$buyer.UserId}|Out-Null
Assert (@(CallApi GET "community/$($post.Id)/comments" $buyer.Token $null).Count -eq 2) 'Comments and replies shared'
CallApi POST 'messages' $buyer.Token @{SenderId=$buyer.UserId;ReceiverId=$seller.UserId;Text='Hello from the shared integration demo'}|Out-Null
Assert (@(CallApi GET "messages/chat/$($seller.UserId)/$($buyer.UserId)" $seller.Token $null).Count -eq 1) 'Conversation visible to the other owner'
$contact=CallApi POST 'contacts' $buyer.Token @{UserId=$buyer.UserId;Name='Demo emergency contact';Role='Family';Phone='09000000000';Address='Demo';IsEmergency=$true}
Assert (@(CallApi GET "contacts?userId=$($buyer.UserId)" $buyer.Token $null).Count -eq 1) 'Emergency contact shared'
$listing=CallApi POST 'marketplace' $seller.Token @{UserId=$seller.UserId;Title="Demo pet bed $id";Description='Integration demo listing';Price=100;Category='General';Condition='Used';Location='Demo';ImageUrls=''}
CallApi POST 'cart/items' $buyer.Token @{UserId=$buyer.UserId;ProductId=$listing.Id;Quantity=4}|Out-Null
CallApi POST 'cart/items' $buyer.Token @{UserId=$buyer.UserId;ProductId=$listing.Id;Quantity=4}|Out-Null
$cart=CallApi GET "cart?userId=$($buyer.UserId)" $buyer.Token $null
Assert ($cart.Items.Count -eq 1 -and $cart.Items[0].Quantity -eq 1) 'Pre-loved cart quantity stays one'
ExpectFailure 400 {CallApi POST "cart/checkout?userId=$($buyer.UserId)&paymentMethod=GCash%20Mock&simulateSuccess=false" $buyer.Token $null} 'Simulated failed payment rejected'
$afterFailure=CallApi GET "cart?userId=$($buyer.UserId)" $buyer.Token $null
Assert ($afterFailure.Items.Count -eq 1) 'Failed payment preserves cart'
$order=CallApi POST "cart/checkout?userId=$($buyer.UserId)&paymentMethod=GCash%20Mock" $buyer.Token $null
Assert ($order.Status -eq 'Completed' -and $order.TotalAmount -eq 100) 'Successful checkout creates completed order'
$cart=CallApi GET "cart?userId=$($buyer.UserId)" $buyer.Token $null
Assert ($cart.Items.Count -eq 0) 'Successful checkout clears persisted cart'
Assert (@(CallApi GET "cart/orders?userId=$($seller.UserId)" $seller.Token $null).Count -eq 1) 'Order visible to seller'
$myListings=@(CallApi GET "marketplace/my/$($seller.UserId)" $seller.Token $null)
Assert (-not $myListings[0].IsAvailable) 'Purchased listing is unavailable'
ExpectFailure 400 {CallApi PUT "marketplace/$($listing.Id)/status?userId=$($seller.UserId)&status=Available" $seller.Token $null} 'Purchased listing cannot reopen'
ExpectFailure 400 {CallApi POST "premium/activate?userId=$($buyer.UserId)&simulateSuccess=false" $buyer.Token $null} 'Premium failure preserves account'
CallApi POST "premium/activate?userId=$($buyer.UserId)" $buyer.Token $null|Out-Null
$premium=CallApi GET "premium/status?userId=$($buyer.UserId)" $buyer.Token $null
Assert ($premium.IsPremium -and $premium.Price -eq 49) 'Lifetime Premium is shared'
Write-Host "Demo buyer: $($buyer.Email) | seller: $($seller.Email)" -ForegroundColor Cyan
Write-Host "Demo passphrase: $password" -ForegroundColor Cyan
Write-Host 'Sign into Web with these accounts and verify the pet, care record, feeding, reminder, conversation, community reply, cart, order, and Premium status.'
Write-Host 'This script creates demo accounts/data in your configured database. It does not remove existing records. Deactivate the demo accounts from Admin after your demonstration.'
