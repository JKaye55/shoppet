# ShoppetCare integration touchpoint journey

Scope: ShoppetCare_Final(4).docx, Version 4.0. Pet Owner and Admin; personal vet reminders only. Payments are simulations. Existing clinic data remains outside the active product flow.

## Start and collect evidence

Stop older API/Web processes first. From the mobile/API repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Integration.ps1 -UpdateClients
```

With `-UpdateClients`, the launcher first fast-forward pulls the web branch and the known separate mobile checkout, then builds Android. It refuses to switch an unexpected branch or reset local changes. Deploy the rebuilt app through Visual Studio.

The launcher locates the web project at the known Visual Studio path, builds and starts API and Web sequentially, checks database readiness, and runs API acceptance checks. It saves a transcript and server logs under `artifacts\integration\<timestamp>`. It creates demo accounts/data for testing and leaves both services running. It does not delete existing records or silently kill processes using the service ports.

If your web folder moved, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Integration.ps1 -WebProject "C:\full\path\Shoppet_VetClinic.csproj"
```

The Android app must be rebuilt/deployed from the latest mobile branch through Visual Studio. Set the emulator API to `http://10.0.2.2:5020/api`. PC Web is `http://localhost:5253`. Keep services running during the journey. For a physical phone or external QR scan, configure the reachable PC LAN URL as described in FINAL_INTEGRATION.md.

## Presentation journey

Use two Pet Owner accounts, Buyer A and Seller B. The automated report prints demo credentials; use a separate Admin account for moderation. Record actual results and screenshots rather than assuming a step passed because it compiled.

| Step | Action | Expected result / evidence |
|---|---|---|
| 1 | Register A on Web; sign into Mobile | Same account and profile; no separate registration |
| 2 | Create a pet in Mobile; reload Web My Pets | Same pet appears |
| 3 | Edit pet on Web; reopen Mobile Pets | Updated name, weight and photo; stable CardId |
| 4 | Add a medication/health record in Mobile; open Web health | Same notes, due date and dose progress |
| 5 | Update feeding on Web; reopen Mobile feeding | Same schedule and last-fed state |
| 6 | Add a personal vet reminder and emergency contact | Same owner records on both clients; no clinic booking implied |
| 7 | Create post in Mobile; comment/reply/like on Web | Shared content and counts after refresh; own edit/delete works |
| 8 | B creates a listing in Mobile; edits it on Web | Same listing, price, photos and availability |
| 9 | B enables/disables seller social links | Link opens configured public destination only when visible |
| 10 | A messages B from the listing | Both accounts see the persisted conversation and context |
| 11 | A adds B's item to cart; open cart on other client | Same listing; quantity stays one; own listings cannot be purchased |
| 12 | Simulate failed checkout | No completed purchase; cart and listing remain available |
| 13 | Simulate successful checkout | Buyer/seller order, transaction and sold status agree; cart clears |
| 14 | Activate lifetime Premium | Premium status shared; expanded pet/record limits |
| 15 | Open public Pet ID / scan QR | Stable pet identity; private care history absent; owner phone opt-in |
| 16 | Sign in as Admin on Web | User/content moderation and transaction review; no clinic workflow |
| 17 | Sign out; browse public pages | Writes require login; About/Help retain landing/home navigation context |

## Verification boundaries

Automated API checks cover shared identity login, ownership denial, pet creation/edit, free pet limit, health, feeding, reminders, contacts, community replies/likes, messaging, listing edits, seller-link privacy, cart quantity, failed/successful checkout, sold-state protection and Premium. The launcher also checks Web/API readiness. These are not a substitute for the visual cross-client actions above, real-device uploads, external QR reachability or Admin UI testing.

Workspace validation for this change: Web, API and Android x64 Debug builds pass; authentication/access contract checks pass. Windows launcher and SQL-backed journey must run on the project PC. No live SQL-backed pass is claimed until its report succeeds.
