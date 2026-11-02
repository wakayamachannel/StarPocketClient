# What PocketRoles and the StarPocket services look at, keep and send

The privacy policy for PocketRoles (the mod, the Client and Aegis) and the StarPocket Games services, written with as little jargon as we can manage.

**Last updated:** 2026-10-16

- Version: 1.3
- Last edited: 2026-10-16
- Takes effect: 2026-11-01 00:00 (Japan time)

> "(planned)" means a feature is not built yet, or has not yet been provided or checked in production. Before that feature is published, we will bring this document into line with the facts.
>
> Support chat and the AI bot remain "planned" in 1.3. The text describes how they will work once provided; it does not mean their production operation has been checked. The conditions for providing the DMs, staff access to appeals and the add-staff candidate list in 1.2 remain in the 1.2 change history. The Japanese version is the original.

## Summary

- We install nothing and check nothing on the PC or phone of people who only join a room.
- Records on a host's PC stay only on that host's PC. They do not reach us.
- Aegis evidence records on a host's PC (the record made when someone is removed or restricted) are kept 90 days for everyone, because appeals can arrive late (the records of a restriction still in effect may stay longer; 3.10). Other records, such as logs, are deleted after 30 days.
- PocketRoles will check (planned), inside that PC, whether your own Among Us account that is signed in is on the shared-ban list. Nothing is sent.
- What reaches us is only what you send yourself (support requests, "Join us" applications, report zips, appeals, applications for a Steam copy) and the information of an account you made yourself. Planned Client sign-in and Friends also send the information described below.
- Support requests are deleted 30 days after being marked resolved (if you write nothing for 7 days after our reply, the request is marked resolved automatically (planned); requests waiting for a reply stay until resolved; emails: deletion after 90 days is planned; not automatic today). Our servers do not store IP addresses. To count repeated sends, a mark made from the IP address that cannot be reversed is kept for 1 day only (it stays in the server backups for as long as they are kept; Article 5). If you write a reply-to email address on the contact page, we email you when we receive the request (planned) and when there is a reply. The emails never contain the subject or message you wrote, or the reply text.
- Server backups keep the newest 14 on the home server and the newest 30 on the author's PC. These are counts: gaps in creation or retrieval can leave backups older than 14 or 30 days. Manual and USB copies currently stay until deleted by hand. Planned deletion is after 90 days for manual backups and on the next copy for USB copies older than 90 days; disconnected USB copies can stay longer (Article 5).
- The support chat at the bottom right of the site (planned) requires signing in with a StarPocket account. It starts with article search without AI. The AI bot can answer people aged 18 or over in an allowed country who consent to sending text to AI (AI starts off). Text written to the bot is sent to Anthropic, a company in the United States, after contact details and similar parts are masked. Chat records not handed to a person are deleted from the server database 24 hours after the chat starts. Even during that time, the author cannot read them on the management screen. Nightly backups retain them for the backup retention period (Article 5). AI drafts for staff replies are off today. Do not write real names, addresses, phone numbers, schools or passwords.
- Chat translation is **off by default**. When it is turned on, the room's chat text is sent to Google or DeepL. You can turn it on from the first screen of StarPocket Client and in settings. That is for a fresh install: for someone who upgraded from v0.5.4 or earlier and never changed this setting, it may still be on (3.9).
- Players removed for a "Certain" cheat in a room are reported to Innersloth (**off by default**). You can turn it on from the first screen of StarPocket Client and in settings. Every way of installing starts off (3.9).
- You do not have to make a StarPocket account for the contact form. Support chat requires creating an account and signing in. If you do, it is not linked to you in the game. If you connect Discord, both your email address and your Discord ID and name are kept.
- Client account sign-in and StarPocket Friends are planned (3.12). Only signed-in Clients send our server the sign-in token, current status and, if enabled, hosted-room details and code. Only approved friends see your display name, avatar, status and shared room, within the age rules. Online status is kept only in memory. Signed-out Clients do not send the new Friends status or room data (sign-in and existing traffic are described in 3.9). People under 13 cannot use Friends.
- To have your records erased, type `/cmd id` in a room's chat and send us the code it shows.
- If you are under 18, read this together with a parent or guardian.
- Support requests are read by the author and by admins and support members chosen by the author. Support members read only requests for their own teams, and only those received after they were added. Support members must be at least 16 and live in Japan; ages 16-17 need parental consent. Help is unpaid. On staff screens, contact details in messages are masked, reveals and views are recorded for 1 year, and watermarks and stops for mass viewing reduce leaks (Articles 2 and 11). However, requests received from support chat, including live conversations, are read only by the author today.
- Room restriction appeals sent through the support site that say the restriction was a mistake are read by the author and by members of the Room Access Team chosen by the author. Staff see only appeals meeting the time conditions in Article 2(4) and submitted through a form with the new viewing notice version 1.2; an unset, empty or invalid access start time, old, missing or unknown versions, appeals failing the time conditions, and requests to erase records or reports about a host sent through the same form, are read only by the author. Staff can write only assessments and a reply draft; only the author decides whether to reply and whether to lift a room restriction (Article 2(4)).
- If you have connected Discord, you can choose "Also tell me by Discord DM when there's a reply". Only if you choose it, our bot sends you a Discord DM with "There's a reply", the receipt number and a link to where you can read it. The reply text is never sent. "Stop all Discord notices" also stops individually enabled notices; resumption starts with the next reply, without resending past notices. Unknown delivery and attempt numbers are kept for 90 days, but result records contain no Discord ID or DM text. Only temporary errors known to be unsent allow attempts at least 15 minutes apart, up to 3 including the first; unknown delivery is not retried. Attempts in progress and unknown deliveries count toward interval and attempt limits (3.1, item 7).
- The author reads in Japanese. Only the requests of people who ticked "may be sent to DeepL for translation" when sending them (this tick is planned) may be translated with DeepL in Germany (the paid DeepL API Pro), when the author or a staff member who can read that request presses "Translate". Parts that look like contact details are hidden before sending, and translations are not kept (3.1, item 12; Article 6).
- Applications to the contributor recruitment ("Join us") are sent while signed in. Every role requires applicants to be 16 or older, and applicants aged 16 or 17 need a parent or guardian's consent. Only the author reads applications; you choose a text or call interview, and calls are not recorded. Unlike ordinary support requests, applications that are not selected or are withdrawn are deleted after 90 days, and those of approved people are kept while they are contributors or support members and for 1 year after they stop (3.13; Article 5).

## Article 1 (About this policy)

1. This policy explains how StarPocket Games ("we", "us") handles information about individuals in:

   1. Software: PocketRoles (the mod), Aegis, today's launcher, StarPocket Client
   2. Websites: the PocketRoles site, the StarPocket Games site, the support site (the Help Centre, the contact page and the account pages)
   3. Support: email, Discord tickets, the contact page on the site (the form and the receipt-number page), the support chat at the bottom right of the site and its AI bot (planned), applications to the contributor recruitment ("Join us")
   4. The StarPocket Account
   5. Central lists: the shared-ban list, the erase list and the lift list in the definitions file
   6. The Discord server "PocketRoles 役職部屋"
   7. The home server: support, accounts, the room list (planned) and so on
2. This policy keeps two things apart.

   1. **Information we handle**: what reaches us (Article 3, sections 3.1 to 3.8, 3.12 and 3.13, including communications to our server listed in 3.9)
   2. **Information a host handles on their own PC**: what the Software records on the host's PC, and what it sends directly from the host's PC (Article 3, sections 3.9 to 3.11)

      - Information kept only on the PC or sent directly to other companies does not reach us. Section 3.9 lists the information sent to our server and its destination.
      - But because we decide the Software's default settings and explanations, we describe it in detail here.

## Article 2 (Operator)

1. Name: StarPocket Games

   - The activity name of one individual developer. Not a company.
   - The author's activity name: もみじちゃ (Momijicha)
2. Name and address: given without delay on request. Contact us through Article 14.
3. Person responsible for personal information: the author
4. Staff: the author, admins chosen by the author (such as the Aegis team), and support members who help with requests. Support members who read requests must be at least 16 and live in Japan. Ages 16-17 need a parent's or guardian's consent. The help is unpaid (3.13). Before including members living abroad, we will change this document and announce it in advance.

   - Admins help with support and appeals, following the author's instructions.
   - Support requests can be read by the author, the admins the author has allowed into the staff page, and support members chosen by the author. Support members read requests within the following limits. Only the author can add staff. The author adds them by entering an email address or Discord ID, by choosing from a list of accounts belonging to people who applied through "Join us" (below the table in 3.2), or by approving someone for support through "Join us" (3.13). They can enter as staff only when signing in by the same method. Requests are split into 9 teams: bugs, room access, help, suggestions, accounts, privacy, reports, parents and guardians, and general support, with names in the form "StarPocket … Team". Support members can read only requests for teams assigned to them. One person may have several teams. Admins can read across all teams (room restriction appeals, as described below, only if they are assigned the Room Access Team). Staff other than the author cannot see requests received before they were added (Article 11). Applications for a Steam copy are read only by the author and admins, never by support members (3.1; applications are not open yet). Until requests can be separated by subject, the general support team (category "Other") is not assigned to support members. "Join us" applications and the author's notes (including interview notes) are read only by the author, not by admins or support members (3.13). This is a separate contact category from the 9 teams. Translation and development contributors do not see support requests. Support members are not shown reply email addresses or account IDs, but can see the display name of people who sent while signed in, to greet them in replies. Email addresses, phone numbers, URLs, IP addresses, account numbers and friend codes in message text will initially be masked. A staff member with permission to read that request must press "Reveal" to see them, and the reveal is recorded (Article 11). This does not reveal the dedicated reply-address or account-ID fields hidden from support members. Please leave out unnecessary personal information. Support members cannot access the room, user or room-restriction management screens (Article 11). Requests received from support chat and live conversations are read only by the author today; admins and support members cannot see them.
   - **Room restriction appeals**: room restriction appeals sent through the support site where "What would you like the author to do?" is "I think the room restriction was a mistake" are read by the author and by staff the author has assigned to the Room Access Team (support members and admins). Even an admin cannot read them without that team. Staff access requires both submission through a form with the new viewing notice version 1.2 and receipt at or after the latest of four times: (1) the appeal access start time explicitly set by the author, no earlier than 2026-10-08 00:00 Japan time, when this policy version 1.2 takes effect; (2) when this feature was introduced on the server; (3) the additional viewing start time for all requests, shared by all staff; and (4) when that staff member was added. Each member's addition time and the shared start times are separate conditions. Lifting a pause does not change the member's addition time or set a new individual viewing start time. If the appeal access start time is unset, empty or invalid, only the author can read appeals. Even if received after introduction, appeals submitted with an old, missing or unknown viewing notice version are read only by the author. Stored notice versions are not filled in with the current version. Introduction time alone does not exclude old forms: the new form's notice version is also checked. Appeals that fail the time conditions are also read only by the author. If the same form is used for "Please erase my records" (an erasure request under Article 12) or "I want to report something about a host" (information about someone else), it is read only by the author, whenever it arrives. Staff can see masked parts (friend codes, evidence numbers, `/cmd id` codes and so on) with "Reveal", and each reveal is recorded. Room codes, in-game names and host names are not masked. Staff can write only assessments (text for the author that you cannot see; each staff member adds them one at a time and cannot change or delete anyone else's) and a reply draft (staff cannot overwrite a draft written by someone else). The author's notes are not shown to staff. Only the author decides whether to post a reply, whether to change the appeal's status or language and whether to lift a room restriction. Staff do not use AI drafts for appeals. Version 1.2 does not change how appeals sent by email or Discord ticket are handled.
   - Admins promise to keep things confidential. There are three parts. They do not tell anyone what they read for this work. They keep the promise after they stop being an admin. If they break the promise, they are removed as an admin.

## Article 3 (Information we handle, and why)

### 3.1 Support (email, Discord tickets, the contact page on the site, the support chat)

"Join us" applications have separate information, access, retention and withdrawal rules in 3.13. The ordinary support-request retention and deletion plans in this section do not apply to those applications.

| Information | How we get it | Why |
|---|---|---|
| What you write, the subject, the category (bug, question, appeal and so on), language | You write and send it | To reply. To review decisions and appeals. To erase records. Anything sent with the subject "Steam copy request" is also used for the purposes in "Applying for a Steam copy" at the end of this section |
| The "The author may use AI to draft the reply" tick | You tick it on the contact page (unticked by default). Kept as it was when you sent, never changed afterwards | To decide whether staff may use AI for the reply draft (item 11) |
| The "may be sent to DeepL for translation" tick, and the version of this policy and the time when you ticked it | Only if you tick it when sending a request (it starts unticked; on the form and in chat, planned) | To decide whether a request may be translated (item 12). So we can show later that you agreed |
| Your choice "Also tell me by Discord DM when there's a reply" (for that request only) | Chosen by someone signed in with an account that has Discord connected, on the contact form or the receipt-number page (it starts as the choice on your account page; it is stored on the request only if you choose differently) | To decide whether to also tell you about replies by Discord DM (item 7) |
| Staff notes and reply drafts (about that request; for room restriction appeals, only the author writes and reads the notes, and the Room Access Team's assessments are added one at a time in a separate box) | Written by staff | Working notes for handling the request, and checking a reply before it is posted. Not shown to you (only text posted as a reply reaches you) |
| Code (R-31 and so on), app version, language | Already filled in when opened from "Contact support" in the Client or on the site. You can remove it | To find the problem faster |
| Names of what was found (tool or DLL file names) | Only when you write them in the "Names shown on the screen" field (optional; the Client never fills them in) | To fix false detections |
| The appeal form's viewing notice version (currently 1.2; separate from translation consent) | The form version is saved when an appeal is sent. A missing version stays missing, and stored records are not filled in with the current version | Together with purpose and time, determines access for Room Access Team staff (Article 2(4)). Old, missing or unknown versions are author-only |
| Friend code (only the appeal form's dedicated field) | You enter it in the dedicated field. Chat's "Ask a person" does not accept appeals and directs you to this form | To match it with Aegis records |
| Attachments (report zips, images) | You attach them | To look into bugs and decisions |
| Email address (only if you give it) | You emailed us, wrote it in the contact form, or sent the request while signed in to an account | To tell you a reply has arrived. A reply-to address also receives a receipt notice (planned; item 7). Sending reply notices to an account email is planned; today only the request's reply-to address is used |
| A mark made from the reply-to email address (planned) | Made automatically when a receipt email is sent (item 9) | To keep receipt emails to the same address to 3 a day (so that nobody can write someone else's address and have many emails sent to it) |
| Discord name and ID | You opened a ticket | To reply |
| Age band (under 13 / 13–15 / 16–17 / 18 or over) (planned) | Chat does not ask for it. It uses the band answered on the account page (3.2). If unanswered, we show where to answer | To decide access to the bot (18 or over only). The band itself is not copied into the chat record |
| Agreed Terms, Policy and screen-explanation versions and times (chat planned) | "Agree and start" keeps the Terms, Policy and screen-explanation versions and time; optional AI agreement keeps its explanation version and time; handing to a person keeps the time. Translation consent keeps this Policy's version and time, for both chat and forms | To show agreement. Chat not handed to a person is deleted 24 hours after its start; handed-over records are deleted with the request. Unknown agreement versions are not added to older records later (backups: Article 5) |
| Receipt number (such as SP-2026-0923-0001), secret-link mark, times and how far you read (planned) | Created automatically. Chat does not give you a secret link; you sign in to open your receipt-number page | So you can read replies |

**Support chat and AI bot (planned)**

This is the chat planned for the bottom right of the site. You sign in with a StarPocket account, and the records are linked to the account number. The contact form can still be used without an account. Items 6, 7, 9 and 10 below also apply to the form; chat-only handling is identified separately. S-14 stops all chat use, form submissions by signed-in users, and "Join us" applications. The email contact remains available (Terms, Article 13).

1. Article search without AI answers first. When the bot is enabled, it answers people who meet the age and country conditions and consent to sending text to AI. AI starts off.

   - The bot uses Claude by Anthropic PBC (United States).
   - The bot is not a person. We say so at the start of every conversation.
   - Article search is not AI. It sends your search text to our server to find help-center articles. Search text and results become chat records (item 8: 24 hours from the start if not handed to a person; backups: Article 5). They are not written to server logs. If you consent to the bot, your text is also sent to Anthropic as described in item 3.
2. The bot answers only from help-center articles. We send the public articles in that language, the language code, and your own text from the same chat (up to the latest 20 previous messages plus the current question, favoring newer text within the character limit). Before sending, we mask parts that look like contact details or numbers, including email addresses, phone numbers, URLs, IP addresses, room codes, friend codes, Discord names or IDs, and receipt numbers; masked parts are not restored. Names within text and anything that cannot be masked are sent as written. Previous bot answers, the author's text, other people's requests, room-restriction and shared-ban records, and account information are not sent. The bot has no tools to read or change records.
3. Your text, after contact details and similar parts are masked, is sent to Anthropic to produce an answer. Previous bot answers are not sent.

   - Anthropic promises not to use content received through its API to train its AI.
   - Anthropic states that it deletes received content automatically within 30 days.
   - However, content judged to break Anthropic's usage rules may be kept for up to 2 years. Safety scores (trust and safety classification scores) may be kept for up to 7 years.
   - Under Anthropic's rules, the answer may be computed in a region outside the United States (storage is in the United States).
4. Before a chat starts, the screen explains records, readers and retention, and shows links and versions for the Terms and this Policy. You press "Agree and start"; the Terms, Policy and screen-explanation versions and time are kept. Eligible bot users see Anthropic (United States), what is sent and how it is masked, and a separate optional "I agree to send text to AI" tick (initially unticked). The country's system, Anthropic's measures and retention periods are explained in this Policy, linked from that screen (Article 6). Without the tick, article search and "Ask a person" remain available. The AI agreement version and time apply to that chat only; we do not add agreement to previous chats.
5. We do not use the bot for the following people or situations. Article search answers, and the author answers through "Ask a person". Article search and "Ask a person" are available regardless of country.

   - Anyone whose age band is not "18 or over", including those who have not answered. Chat uses the account-page band without asking for age again
   - Countries and regions absent from [Anthropic's published supported-country list](https://www.anthropic.com/supported-countries), including mainland China, Hong Kong and Macao; all of Ukraine; Kosovo; an unknown country (including Tor); and any additional country or region we block. Only countries allowed from the published list receive the bot. We use only Cloudflare's country mark, never browser time zone or language. The country mark is not stored
   - Monthly cost, per-chat cost, per-person daily (UTC) usage or service-wide daily AI usage limits; AI downtime; timeouts or unusable answers. We reserve the maximum cost first, so AI may be unavailable before actual spending reaches the limit. Failed answers count too. Article-search results are shown for that question. If an AI answer was attempted but unusable, a notice explains this
   - AI is used for staff reply drafts (item 11) only for requests where "The author may use AI to draft the reply" was ticked when sending. Even if ticked, the text of people under 16 and people using it from mainland China is not sent (item 11; the same as in 1.0)
6. "Ask a person" sends a short description and other details as a support request. It does not accept room restriction appeals; it directs you to the appeal form. If the author has marked themselves available, you enter a waiting room; once accepted, you exchange messages on the same screen, regardless of language. The current wait is 5 minutes. If the author does not accept, is unavailable, or chooses to handle it as a request, you go to the receipt-number page for a later reply. "Cancel" in the waiting room does not delete the request; it marks it resolved as cancelled by you. Chat requests do not give you a secret link.

   - For form requests, staff Discord receives only the receipt number, category and language, never the subject or message. For chat requests, only the author's Discord DM receives fixed text about arrival, waiting or additions, and a management-page link containing the internal request number. The receipt number, category, language, subject, message and search text are not sent. Live conversation messages do not trigger notices. If the email saying Discord was connected fails, the account number (starting with u_) is also sent so the author can notice and tell you manually.
   - Today, only the author reads and answers requests from chat and live conversations. Admins and support members cannot see them. Before giving staff access, we will change this document and announce it in advance. Availability uses a mark the author changes, not whether you or the author have a page open. The mark, waiting deadline and last message time are stored on the server.
7. Live and later replies are kept as the same request. For chat requests, you sign in to read your receipt-number page and "Your requests"; there is no secret link. Form requests can also be read through a secret link. When the author ends the chat, the request is marked resolved. If you choose to wait for a later reply, or neither side sends a message for 15 minutes, you move to the receipt-number page with the request unresolved. If you write nothing for 7 days after our reply, the request is marked resolved automatically (planned). For 48 hours after resolution, you can add text on the same page.

   - Only if you wrote an email address on the form, we also email that a reply has arrived. Chat has no reply-to field, so today neither receipt nor reply notices are emailed. Live messages trigger neither email nor Discord DMs, and these notices are not sent later after the chat ends. Emails contain neither your subject or message nor the reply text.
   - If you sent from the form while signed in and the account has an email, it will also receive the reply notice (planned; only one email if both addresses match). For Discord-only accounts, only the reply-to field is used. Today only the reply-to address receives it.
   - If you wrote a reply-to email address on the contact page, we also email that address to say we received the request (planned). It contains fixed text and the receipt number, and will include the team name (planned). The subject and message you wrote are not included. In case someone wrote another person's address, it also has the line "If this doesn't ring a bell, you can ignore this email." At most 3 receipt emails a day go to the same address (item 9).
   - The reply email shows the name of the person handling it and the name of the team decided by the request's category (9 teams; Article 2(4); planned). The handler's name is the display name of the staff account that wrote the reply, not a real name. The handler's icon and a stamp (pictures we made) are added too (all planned). The email opening also gives the team name, not your name (planned). This is staff information, not additional information about you.
   - Only support members assigned to the request's team may help with it (Article 2(4)). Staff may use reply templates, but a person checks the content before replying. At first, every reply from a support member is checked by the author before it reaches you (Article 11). Only the author posts replies to room restriction appeals. Chat requests are excluded from this support-member access; only the author reads and answers them.
   - **Discord DM notices**: if you are signed in with an account that has Discord connected, you can choose "Also tell me by Discord DM when there's a reply" (on the contact form, the receipt-number page and your account page). Only if you choose it, our bot (the bot StarPocket runs on Discord) sends a DM to your Discord when a reply arrives. If you choose it on your account page, replies to your "Join us" application are announced the same way (3.13). If you are under 16, please decide together with a parent or guardian (this is also written where you choose; Article 10). Live chat messages do not trigger DMs, including after the chat ends. Later replies after the chat ends follow these DM settings.

      - The DM contains only the fixed text "There's a reply", the receipt number, a link to where you can read it ("Your requests") and how to stop these notices. It never contains the reply text, the subject, your message or the handler's name (the same rule as email notices).
      - To send the DM, your Discord user ID (as the recipient) and the DM text are passed to Discord Inc. (United States) (Article 6). Where you choose also says "Sent to Discord Inc. (United States)" and links to this policy.
      - The DM only arrives if you are in our official Discord server and allow DMs from server members. Even if it does not arrive, you can read replies on the site. After 3 failed deliveries in a row, we send no DMs for 7 days from the last failure (so Discord doesn't see us as a nuisance). Your account page shows this; once you fix the setting, press "Resume from the next reply". Notices resume when the next reply arrives. Past notices are not sent again.
      - To avoid sending too many, DMs for the same request are at least 15 minutes apart, with a daily limit per person. Only temporary errors where the notice is known not to have been sent allow another attempt, at least 15 minutes later, up to 3 attempts for the same notice including the first. If a timeout or connection loss after sending leaves delivery uncertain, it is recorded as "delivery unknown" and the same notice is not resent. Attempts left in progress after a restart are also treated as delivery unknown. Attempts in progress and unknown deliveries count toward the 15-minute interval and attempt and quantity limits. Restarting does not reset the attempt count. Past notices from the paused period are not sent again either.
      - When Discord is unlinked (for example at your request through the contact in Article 14) or another Discord is linked, this choice is cleared. We send to the new Discord only if you choose again.
      - We keep records of sending attempts and DMs sent, not delivered, delivery unknown or not sent under these rules (times including the sending start, attempt number, which reply, request and account numbers, the result and a short reason code) for 90 days. The attempt number and sending start are recorded before communication. This record does not contain your Discord ID or the DM text (Article 5).
      - You can stop at any time on the receipt-number page or your account page. Even if your account-wide choice is off and only individual requests are on, you can press "Stop all Discord notices" on your account page. This clears individual choices too and stops all Discord notices, including those for "Join us" applications.
      - We only send these DMs if you choose them. We keep your choice (and, if you chose on your account page, when you chose it) as your consent to sending to Discord (Article 17(3)).
   - For a request you sent while signed in, the email has a way into the "Your requests" list (planned; opening it needs sign-in). If you sent it without signing in, the email gives only the receipt number. Either way, the secret link is never put in an email.
   - If someone else saw your secret link, "Renew secret link" on the receipt-number page makes the old link stop working (planned).
8. Chat records not handed to a person (your questions, search results, bot answers and notices such as AI unavailability, account number, agreement versions and times) are deleted from the database 24 hours after the start, not after the last use. During that time, the author cannot read them on the management screen. Deleting the account also deletes them. "Attach the conversation so far" in "Ask a person" starts ticked and can be unticked. Attached records are copied into the request, read by the author, and kept for the request's retention period (AI answers remain marked). Search records on the chat side are deleted immediately on handover, whether attached or not. Agreement and related records are deleted with the request. Other records contain no message text: daily usage totals, an AI cost ledger (no automatic deletion; unlinked when the chat is deleted), per-person AI counts (until the next UTC day), and chat usage counts (24 hours). After database deletion, nightly backups retain records for their own retention period (Article 5).
9. To prevent abuse, we use:

   - a bot check (Cloudflare Turnstile)
   - sending limits
   - To count sends, the IP address is mixed with a random secret that changes each time the server restarts, turned into a mark that cannot be reversed, and kept with the request and sign-in records for 1 day only. The IP address itself is not kept. After 1 day the mark is deleted from the server's database. It does stay in the server backups (the last row of Article 5) for as long as they are kept.  Requests made from chat do not include this IP mark. Chat rate records instead keep the account number and time for 24 hours (Article 5).
   - To keep receipt emails (item 7) to 3 a day per address, the reply-to email address is mixed with a secret that exists only inside the server, turned into a mark that cannot be reversed, and kept for 1 day (planned). The mark cannot be turned back into the address. After 1 day it is deleted from the server's database. Like the IP mark, it stays in the server backups for as long as they are kept.
10. The server never opens URLs written in messages.
11. **Staff reply drafts and translations (AI)** (off today; we will announce it before we start using it)

    - Staff may have AI (Claude by Anthropic PBC, United States, used through its API) write drafts or translations of replies.
    - Something is sent to the AI only for requests where "The author may use AI to draft the reply" was ticked when sending, and only when a staff member presses the "Draft with AI" button. A request without the tick is not sent even if a staff member presses the button. Nothing is sent automatically. Even with the tick, the text of people under 16 and people using it from mainland China is not sent.
    - What is sent: the subject and text of that support request (the conversation so far), its category, language and the "Game info" field (app version, codes such as R-31)
    - Friend codes, email addresses, URLs and long numbers written in the text are hidden before it is sent (the same as 3.5, item 5).
    - What is not sent: email addresses, Discord names and IDs, friend codes (whether from the special field or written in the text), attachments (report zips, images), receipt numbers
    - A staff member always reads and edits the draft, and a staff member sends it. The AI never sends a reply by itself.
    - The AI is never allowed to decide room restrictions, shared bans, appeals, limits on the Services or erasing records. Staff always decide those.
    - How Anthropic handles it (not used for training, days until deletion) is the same as for the bot in item 3.
    - Chat has no tick for staff AI drafts, so requests sent from chat are not used for those drafts, regardless of age. Requests received by email or Discord ticket are not sent to AI until the basis for sending abroad (Article 6(3)) is settled.
12. **Translating support requests (DeepL)**

    - The author reads in Japanese. To read requests written in other languages, and to write replies in the sender's language, we may translate the text of requests received through the support site (https://support.starpocketgames.com) with the paid translation service of DeepL SE (Germany), DeepL API Pro. Requests that came by email or Discord ticket are not translated with this feature.
    - We translate only requests sent with "may be sent to DeepL for translation" ticked. The box starts unticked; you can send without it. Requests without the tick, including those received before the box existed, are not translated. The box is provided on the contact form and in chat's "Ask a person" (planned).
    - Next to the box we show that text goes to DeepL SE (Germany), that Germany (EU) is recognised as protecting personal information at a level similar to Japan, and that DeepL states the paid DeepL API Pro does not keep translated text or use it for training (planned; Article 6(3)).
    - Ticking keeps this Policy's version and time with that request. Text you add later may also be translated under that tick. You can untick it on the receipt-number page; after that, no translation takes place. You cannot tick it again for the same request (planned).
    - Text is sent only when the author, or a staff member who can read that request, presses "Translate". Nothing is sent automatically. Support members can translate only requests for the teams they handle (for Room Access Team staff, this includes the room restriction appeals they can read; Article 2(4)).
    - What is sent: only the text you wrote, or the text of a reply draft written by staff, and the languages to translate between. Before sending, parts that look like contact details or numbers are hidden: email addresses, phone numbers, URLs, IP addresses, friend codes, Discord and other IDs, ticket numbers and so on. Parts opened with "Reveal" on the staff screen are still sent hidden (Article 11). Anything in the text that cannot be hidden, such as names, is sent as it is. The reply email address, the account and attachments are not sent. The same masking applies when the author presses the button.
    - The translation is only shown on screen and is not kept on our servers. If staff check it, put it into a reply draft and send it as a reply, it is kept like any other reply.
    - The only record kept is who translated how many characters of which request (Article 5). Each person can translate up to 20,000 characters a day.
    - Only the paid DeepL API Pro is used; "Translate" does not work with an API Free key. DeepL states that with the paid API Pro, text it receives is deleted once translated and is not used for training or to improve its services ([Privacy Policy](https://www.deepl.com/en/privacy), [API plan information](https://support.deepl.com/hc/en-us/articles/360021200939-DeepL-API-plans)).
    - This is separate from the AI (Anthropic) in item 11. Translating "Join us" applications is covered in 3.13. It is also separate from the translation for the Steam-copy program at the end of this section.
    - Replies may also use Japanese, Simplified Chinese and English templates: choosing one in Japanese displays the recipient's language. A person checks it before sending.

**Applying for a Steam copy (for people who want to host)**

1. We plan to give up to 10 Steam copies of Among Us to people who want to host. We will announce the day requests open in a notice on the site (planned). Requests are open for 30 days from that day (they close sooner if all 10 are decided). This is not a lottery: the author reads the requests and decides. Donating, following and so on are not conditions, and donors get no priority.
2. What we receive: what you send on the contact page with the category "Other" and the subject "Steam copy request". The answers are letters: where you have Among Us now, roughly when and with whom you plan to host, whether you have a Steam account, whether you could tell us once what you thought after playing, your age band (16-17 / 18 or over), your Steam store country or region, where you heard about PocketRoles, and whether you agree to this explanation. Only people the author would like to choose are asked, on their receipt-number page, for their Steam friend code. After becoming friends, the author looks at your Steam profile for when the account was created and how many games it has (no copies are kept). We don't need your real name, address, phone number, school, social media or WeChat IDs, or passwords.
3. Who reads it: only the author and the contact page's admins. Until requests can be separated by subject, the general support team is not assigned to support members (Article 2(4)).
4. Purposes (only these): (1) choosing who receives a copy; (2) becoming Steam friends, sending the gift and checking it was accepted; (3) making sure nobody gets two copies; (4) receiving your impressions or bug reports after playing (a request, not a condition); (5) counting where people heard about PocketRoles (the tally keeps only the numbers).
5. What we don't do: no AI decides who is chosen. Requests aren't used for AI reply drafts, even if "The author may use AI to draft the reply" is ticked. We don't look up your other support requests, Discord, GitHub or video-site accounts to combine them with your request. Nothing is sold or passed on.
6. Translation: the author reads only Japanese. Anything written in another language may be run through machine translation by DeepL (DeepL SE, a company in Cologne, Germany) so the author can read it. Only that text is sent to DeepL, and the receipt number, Steam names and friend codes are removed first. Germany is in the EU, which Japan's Personal Information Protection Commission recognises as having a system of the same standard as Japan's. This program uses paid DeepL Pro. DeepL states that it deletes text after translation and does not use it for training or service improvement. For this Steam-copy program, DeepL Pro is used only during the 30-day application window, separately from support translation in item 12.
7. Steam: friend requests and gifts go through Steam (Valve Corporation, USA). The only thing the author picks on Steam is your account. Your request isn't given to Valve.
8. These purposes apply only to requests sent after reading and agreeing to this explanation on the guide page. Information from people who did not apply (other support requests and so on) is never used for them.
9. How long it is kept: the request and the messages on your receipt-number page stay on the author's home server in Japan and are deleted automatically 30 days after we tell you the result and mark it resolved. The same applies to people who weren't chosen. Within 7 days of the close, every request still open gets a reply and is marked resolved. The author removes the Steam friendship once the gift has been accepted. The author's Steam gift history and purchase records (including the purchase confirmation email) keep who received a gift, as part of how Steam works, and the author cannot delete them. The author keeps these purchase records for tax. The new-request notice sent to the author's Discord contains only the receipt number, category and language.
10. Age: we can't accept requests from people under 16. Steam accounts are only for people 13 and over, and Article 5 of the Terms says that when consent is needed for the personal information of someone under 16, we get a parent's or guardian's consent. This programme has no way to check that. If you are 16 or 17, please tell a parent or guardian before applying.
11. Cancelling: write "cancel" on your receipt-number page at any time. We mark it resolved straight away, and it is deleted 30 days later.

### 3.2 StarPocket Account

The contact form needs no account; support chat requires signing in with a StarPocket account (Terms of Use Article 8).

| Information | How we get it | Why |
|---|---|---|
| Email address | Written by people who sign in by email, on the site or in StarPocket Client (planned) | To send the 6-digit verification code and notices. Sending reply notices to the account email is planned; today they go only to the address in the request's reply-to field (3.1, item 7). Only for "Join us" applicants, the author uses display names and sign-in methods in the add-staff list for selection and registration (below; 3.13) |
| Display name | For Discord sign-in, the Discord display name is used as it is; for email sign-in, it is empty. Choosing or changing it yourself, on the site or in the Client, and keeping the date you chose it are planned (it need not be your real name) | To show on screen and to approved StarPocket Friends (3.12; planned). Friends requires a name you chose yourself; the Discord name is not used automatically. Only for "Join us" applicants, the author uses display names and sign-in methods in the add-staff list for selection and registration (below; 3.13) |
| Account creation and last sign-in date and time | Automatic. Client sign-in will keep dates only, without times (planned) | To delete accounts unused for 2 years (planned; no automatic deletion today). Only for "Join us" applicants, the author views last sign-in dates in the add-staff list for selection and registration (below; 3.13; Article 4(10)) |
| Receipt numbers of linked requests | When you contact support while signed in | To show your requests together under "Your requests" on the support site |
| Discord user ID and name | Only people who sign in with or connect Discord; this will include Client sign-in (planned) | Sign-in and reply notices. Connecting Discord sends a notice to the account email and keeps both the email and Discord ID and name. To disconnect, ask through Article 14 for now (a button is planned). If you choose "Also tell me by Discord DM when there's a reply", DMs are sent to this ID (3.1, item 7). Only for "Join us" applicants, the author uses display names and sign-in methods in the add-staff list for selection and registration (below; 3.13) |
| Your choice "Also tell me by Discord DM when there's a reply", and when you chose it | When someone who has connected Discord chooses it on the account page (it is also the starting choice on the contact form) | To decide whether to also tell you about replies to all your requests by Discord DM (3.1, item 7). So we can show later that you chose it |
| Passkey public key and ID (planned) | Only people who register a passkey | Sign-in (fingerprint and face information never leaves your device) |
| Sign-in records (time, rough browser type such as "Chrome on Windows", whether the device is new) (planned) | Automatic (IP addresses are not stored) | To tell you about unknown devices and show "Signed-in devices" (device type, first and last use, this device) and "Sign out all other devices". Client sign-ins and new-Client notices follow the same rules (planned) |
| Device mark (a number in the browser) (planned) | Automatic | To identify new devices. One notice per new device, never again for that device; at most 5 per person per day, with excess combined the next morning. At the server-wide daily limit, notices stop and verification codes take priority |
| Confirmation of making an account with a parent or guardian (planned) | Ticked by people under 18 | To confirm they made it together. The "I will use this with my parent or guardian" check for ages 16-17 before using Friends is handled here too (planned). The account age field and an optional "I will use this with my parent or guardian" mark for people under 18 are planned to be introduced in production together with support chat. This is not a condition for chat use |
| Age band (under 13 / 13-15 / 16-17 / 18 or over) (planned) | You pick a birth year on the account page to determine bot eligibility and before using Friends. The browser calculates the band; the year itself is neither sent nor kept. We keep the band, answer date, date you can answer again (30 days later), lowest band ever answered and the year of that answer, to prevent answers that raise your age too far | To decide access to the support-chat bot (18 or over only), parental confirmation (Article 10), access to Friends (unavailable under 13), and who can upload and see images (3.12) |
| Agreed terms and policy versions and time (planned) | Kept when you press "Agree and create"; Client sign-in also supplies its agreed version (planned) | To show agreement and prevent Friends traffic from a Client without the required agreement |
| Service-limit records (Terms Article 13: code, rule, end date, author's note), and account suspension time, reason and note | When the author decides a limit or suspension | To apply and review it. You are told the code, rule and end date, but not the note |
| Email address change (old and new addresses) (planned) | When requested | To notify the old address and allow cancellation. Deleted 30 days after completion |
| Client sign-in token (in an irreversible form), app name and version, sign-in method (email or Discord), sign-in date and last-use date (dates only) (planned) | Automatically at Client sign-in | To keep you signed in, list the Client under signed-in devices and allow sign-out there. The token expires 90 days after sign-in, even if you keep using it. Its server record is deleted 1 day after expiry, sign-out or revocation |
| One-use exchange token for Discord sign-in from the Client (planned) | Automatically during sign-in | To return the browser's authorization safely to the Client; expires in 60 seconds |
| Parent's or guardian's email (ages 13-15 only; planned) | Entered once on the account page | To send a one-use confirmation link valid for 24 hours. The email is deleted on confirmation or expiry; only the confirmation date remains (Article 10) |
| Avatar (a supplied picture number, or your uploaded image converted to a 256×256 PNG without the original location or device metadata, and its fingerprint) (planned) | Selected or uploaded in the Client | To show approved friends under the age rules (3.12), and prevent reupload of removed images using fingerprints |

- **Your account is not linked to you in the game.** Your Among Us account, friend code, PUID, in-game name and Aegis records are not put in the account.
- When you join a friend's room, you see each other's in-game names through Among Us itself; StarPocket Friends does not link those names to accounts (3.12; planned).
- We do not use accounts to show ads or give information to other companies.
- The list for adding staff (Article 2(4)) includes only people who applied through "Join us". People who have signed in but have not applied are not listed. Only the author sees the list: display name, sign-in method (Discord name and last 4 ID digits, or a partly hidden email address), and last sign-in date. These are used for applicant selection and staff registration (3.13; Article 4(10)). Suspended accounts and existing staff are not listed. A person becomes staff only when the author adds them or approves their application.

### 3.3 Report zips and "One player's evidence" zips

1. Pressing "Make a report zip" in the launcher or the Client makes a zip on the host PC's desktop. **It is never sent automatically.** You decide whether to send it.
2. Contents

   - Game logs, launcher records, PC and game versions
   - Aegis evidence records from the last 90 days (the newest 200, up to 5 MB; older records go in a "One player's evidence" zip, item 5), and, for records older than 30 days, the two game-log lines that back each one (in place of the deleted log) (3.10)
3. Left out or masked

   - Left out: API keys, Discord webhooks, settings files, passwords
   - Masked: PUIDs, friend codes, the start of hashes, erase codes (in logs), places containing the Windows user name
   - Evidence records contain the player's name, the hash of their PUID and their erase code (not the friend code or its hash).
   - The code is left unmasked so the author can handle erase requests. Matching it against the erase list and erasing that person's records kept by the author and admins is planned (today's ban console does not match against the list yet; 3.5, item 4).
4. A zip also contains the names of other people who were in the room, and the codes of the people in its evidence records. Send it by email; do not post it in public places.
5. **"One player's evidence"** (for appeals): when the host enters a person's erase code, friend code or evidence number (AEG-…) under "One player's evidence" in the launcher (in the Client, in the report area), a small zip with only that person's records is made on the desktop. The author asks the host to make it when looking into an appeal, and the host sends it to the author by email. **It is never sent automatically.** A host can also make one at any time, unasked, for any player whose records are on their PC (what goes in is what that PC already holds). Passing it to anyone but the author is forbidden by the Play Rules, Article 7. When you appeal, the author receives every record of you on that host's PC (not only the restriction you appealed, but also records of a removal alone).

   - Contents: that person's evidence records still on the host's PC (of records older than 90 days, only the evidence of a restriction still in effect), the game-log lines that back them, and the PC and game versions (the same `system.txt` and so on as in a report zip, so the ban console can tell zips from the same PC apart). Records an erase request covers (already emptied, or listed on the erase list) are left out.
   - Other people's records and logs are not included (the detection text of that person's record may still name other players of the same match).
   - The erase code in the records is what says which records an appeal is about: the author compares it with the code the appealing person saw with `/cmd id`. **That code does not prove who sent the appeal** (Article 12(5): it is not a password; in an unregistered room everyone sees it, and it is in hosts' records and report zips, so other people may know it). So a matching code alone never makes the author tell anything from the records (the reason, dates, room codes, other people's names). To check further, the author uses the extra questions of Article 12(2) (their answers are exactly what this zip holds, so when a copy of the zip is in someone else's hands, matching answers are not taken as proof either). Neither the friend code nor its hash is included, also when the host searched by friend code (a friend code's hash can be turned back by trying them all, so including it would not protect anything).
   - Masking is the same as for report zips. Like report zips, the launcher deletes it automatically after 30 days.
   - Send this zip to the author only (Play Rules, Article 7(1)). It holds that person's erase code and the hash of their PUID, which the restriction list uses.

### 3.4 Central lists (lists anyone can read)

They are in the definitions file (public on GitHub, signed). Details are in Article 8.

| List | What is on it | Why |
|---|---|---|
| Shared-ban list `[bans]` | PUID hash, level, end date, short reason word | To restrict the same person in every PocketRoles room. Also, PocketRoles checks whether your own account signed in on that PC is on it (planned; compared only inside that PC; 3.11) |
| Erase list `[erase]` | Erase codes and dates | To erase records on each host's PC |
| Lift list `[unban]` | The erase code (the `/cmd id` code) and the evidence number (AEG-…) and date of the record we checked | To lift, on each host's PC, automatic restrictions found to be mistakes. For 30 days from listing, Aegis does not restrict that person automatically. Listed for at least 180 days |

- Names and friend codes are not on them.

### 3.5 Ban console (the author's and admins' PCs)

1. Evidence records from report zips and "One player's evidence" zips (3.3) sent to us are imported into the ban console on the author's and admins' PCs.

   - Imported evidence records contain the player's name, the hash of their PUID and their erase code (3.3). So the author and admins also receive the codes of other people, not only of the person who sent the zip.
   - The codes are used to match against the erase list, so the records of people who asked for erasure are erased (today's ban console does not match them yet; see item 4).
2. The action record (`audit.log`) keeps:

   - time, action, evidence number, name, rule, reason, who proposed it
   - Friend codes and PUIDs are not kept.
3. Why: to decide shared bans and appeals correctly and in a way that can be checked later.

   - When a shared ban is based only on records sent by other hosts, these rules apply (Play Rules, Article 9(2)).

      - A "Certain" detection whose record matches that host's game log: one host's record is enough, but only for the first level (up to 30 days); at most 2 per 30 days from one and the same host
      - Anything else (a "Repeat" detection, chat, hurtful words, harassment, or a restriction the host decided by watching): records from 2 or more different hosts received within 30 days, or a report from a trusted admin; or the author writes down the reason and keeps it in `audit.log`
      - The three lines here count different things. The **hosts**: 2 or more different hosts (this may go up as more hosts join). The **bans**: at most 2 per 30 days from one and the same host. The **mistakes**: 3 times within 30 days, on the next line.
      - If a host's reports are found to be wrong on appeal 3 times within 30 days, we stop accepting reports from that host (mistakes of "Certain" detections do not count; Play Rules, Article 7(3); Terms of Use Article 13; S-11)
   - For this, when the author lifts a shared ban as a mistake, a record of the hosts who sent the original reports is kept (the report-host record `reporters.txt`, and lines in the action record `audit.log`). It holds the host's name (the name the author gave the report zip when importing it), a PC mark (a short mark made from files inside the zip, to tell zips sent from the same PC), the date, the evidence number and which lift it was. It is used only for counting. Lines in `reporters.txt` are deleted after 30 days. Lines in `audit.log` are handled like the action record in item 4.
4. Today, imported records and the action record have no expiry, and they are not yet matched against the erase list. We plan the following, and will update this document on the day it is built (planned):

   - delete them 90 days after the case ends (planned)
   - erase records for codes on the erase list (planned)
   - make names in the action record maskable (planned)
5. Reply drafts (AI): only when the author, unlocked as the author in the ban console, presses "Draft with AI", the reply's kind, language, ban number, rule category, dates and template are sent to Anthropic PBC (United States) to make a draft.

   - The addressee's and the staff member's names are replaced by placeholders (NAME_1, STAFF_1), and so is a shared ban line's number (the first 8 hex digits of a hash; BANID_1); they are put back into the returned draft on the author's PC.
   - PUIDs, friend codes, hashes and evidence files are not sent.
   - The box for pasting the support request's text is not shown until the basis for sending it abroad (Article 6(3)) is settled. Once it is used, friend codes (their name part too), e-mail addresses, URLs, @names and long numbers are hidden and the addressee's, the recorded and the staff member's names become placeholders before it is sent; anything else in the text that cannot be hidden, such as other people's names, is sent as written. Before it first goes out, the author sees exactly what is sent. Text from people under 16 and from people in mainland China is not pasted (3.1, item 5). Text of requests that came by e-mail or Discord ticket is not pasted until the basis is settled.
   - The draft is checked on the author's PC (a draft that does not keep the template's result paragraphs word for word, has words against the result, has a number, date, e-mail address, URL or the like that the template does not have, or leaves out the template's number or dates is not used), and a person sends the reply. The ban console never sends a reply.
   - Anthropic's handling (no training, deletion period) is the same as in 3.1, item 3.

### 3.6 Websites

1. The official site (starpocketgames.com) and the PocketRoles site (pocketroles.starpocketgames.com) are both on Cloudflare Pages (Cloudflare, Inc., United States). The support site and the account pages are served by the home server (3.8) through Cloudflare.

   - Cloudflare states that it records visitors' IP addresses for security. So does GitHub, where downloads and the definitions file are kept.
   - Text uses the fonts already on your device (computer or phone). Nothing is loaded from other sites or companies, fonts included (no Google Fonts). The exception is the bot check (Cloudflare Turnstile), loaded from Cloudflare on the contact page, account pages and support chat (planned; item 4).
2. The site stores only the following in your browser:

   - the language you chose (on the support site, opening an article page is enough for the language to be remembered in a cookie for 1 year)
   - light or dark display (official site)
   - what the contact page and the account pages need to work (Article 16(2))
3. We use no ads, no analytics and no social media buttons.
4. On the contact page, account pages and support chat (planned), Cloudflare Turnstile runs as a bot check (3.1, item 9). Client sign-in uses request limits instead of Turnstile (planned).

### 3.7 Discord server

1. The Discord server runs on the service of Discord Inc. (United States). Discord's privacy policy also applies.
2. What we see:

   - names, messages and roles in the server
   - what is written in tickets
3. Ticket transcripts are set not to be kept. Appeal tickets are deleted when decided.
4. StarPocket Client shows only Discord's "online now" count. It does not collect members' names. The request that gets the count is in 3.9.

### 3.8 Home server

| Information | Why | How long |
|---|---|---|
| Rooms listed in the room list (planned; today nobody can list a room yet) (room code, server region, player count and capacity, room state (open, full, in game), room kind (with roles or normal rules), map, language, chat type (free or quick), mod and game versions, tags (chosen from a list)). No names, friend codes or free text are sent | To show the room list | Deleted when the room closes or is made private. Memory only |
| Host key (the public half) (planned) | To prevent impersonation in the room list | Deleted after 30 days unused. `/rooms forget` deletes it at once |
| Host events (key number, kind, region, time; no room code) (planned) | To look into abuse | 7 days |
| Records of room-code conflicts and of rooms hidden by staff (with the room code) (planned) | To look into abuse | 7 days |
| Support requests (3.1), accounts (3.2), "Join us" applications (3.13) | As in 3.1, 3.2 and 3.13 | As in Article 5 |
| Server operation logs | To look into faults | 14 days. No IP addresses or message text |

- A room is listed only when the host chose "Agree" in the settings.

### 3.9 What the Software sends from the host's PC

The table lists traffic from the host's PC. Traffic addressed to our server reaches us; traffic sent directly to another company does not.

| What is sent | To | When | How to stop it |
|---|---|---|---|
| Requests to receive the definitions file (only receiving; nothing about this PC is sent) | GitHub (United States) | At game start, and once a day while running. When the tray Aegis starts. Also, when a room could not be created because an update is required, and when the online menu is opened during that time, at most once every 5 minutes (the same will apply when not creating rooms during a shared ban (planned) is added) | Cannot be stopped (as with any connection, GitHub receives the IP address). `RemoteRules=false` only stops the received detection numbers from being used; the file is still received for the erase list (and, once checking this PC's own account (3.11; planned) is added, for that too). The tray Aegis receives it whatever the setting |
| Chat text (up to 300 characters) and the target language | Google (United States) or DeepL (Germany) | When chat translation is used. Names and room codes are not sent | Untick "Use chat translation" on the Client's first screen. "Chat translation" in settings. `/opt translate off` |
| Room code, player count, state | The Discord webhook the host set | Only when the host set it | Do not set it |
| Official report (reason and the player) | Innersloth (United States) | When a player is removed for a "Certain" cheat (at most once every 30 days per player). On `/aegis report` | Turn it off in settings under "PocketRoles → Aegis", or with `[AntiCheat] AutoReport=false`. **The default in v0.5.5 is off.** The key `[AntiCheat] AutoReport` existed in no build up to v0.5.4, so an upgrader's config file does not hold it either: every way of installing starts off |
| Requests to get Discord's "online now" count (no names come back) | Our server (we do not connect to Discord directly) | When the Client's community panel is opened (planned). Our server gets the count from Discord once every 5 minutes and passes it on | Do not open the panel |
| A check to choose a region | Among Us servers | Only when `AutoRegion` is on | Off by default |
| Update checks, downloads | GitHub, BepInEx's download site | When you press "Check for updates", Install, Repair or "Update". **The Client does not check by itself at start** (Terms of Use Article 12(2)(2)) | Do not press |
| Requests to receive news and status notices (planned; today's Client fetches nothing at start) | Our server (through Cloudflare) | At Client start (planned) | — (only receiving; nothing is sent) |
| Account sign-in token, current status (Client open or playing PocketRoles), and, if room sharing is enabled, hosted-room details (map, players, capacity, state, public/private, region) and code; no names (planned; 3.12) | Our server through Cloudflare | While signed in, every 30-120 seconds (30 seconds while sharing a room). Playing the ordinary Steam copy of Among Us is not reported | Sign out, hide your status, turn off room sharing, or choose not to share this room just after creating it. Signed-out Clients send none of this |
| Email address and 6-digit code for email sign-in; for Discord sign-in the browser visits Discord and our server and returns to the Client through this PC (127.0.0.1) (planned) | Our server through Cloudflare; Discord (United States) | When you sign in from the Client | Do not sign in |
| Display-name changes, avatar selection and uploaded image, friend-card creation and entry, approval, declining, removal, blocks, reports and receiving room codes (planned; 3.12) | Our server through Cloudflare | When you press the relevant button | Do not press it |
| Requests for friends' avatar images (receiving only) (planned) | Our server through Cloudflare | When a friend's image changes | Stops when signed out |
| One sign-out request before uninstall (planned) | Our server through Cloudflare | When a signed-in user uninstalls; only after agreeing | Sign out beforehand |

- As with any connection, the other side's server receives the host's IP address.
- **Checking your own shared ban** (planned): PocketRoles in the game uses the definitions file it received to check, inside this PC, whether the Among Us account signed in is on the shared-ban list (3.11). Nothing new is sent for this.

   - Also checking with our server (by receiving the whole list) is **not done now**. If we start it, we will first change this policy and ask for consent again in StarPocket Client (Article 17(3)). The PC of anyone who has not agreed does not make these requests.
   - In the planned form, nothing about your account would be sent: PocketRoles would receive the whole list from our server and compare it inside this PC. As with any connection, the IP address would reach our server and Cloudflare.
   - We will not use a form that asks "does this account have a shared ban?" (sending the first 5 characters of the hash of the PUID and a number that changes every time). Because the list's hashes are public, the server could tell who is asking when someone on the list asks.
- **How chat translation is decided**

   - In StarPocket Client, the first "Terms of Use and Privacy Policy" screen has a "Use chat translation" checkbox. It starts unticked. Unless you tick it, nothing is sent (someone who upgraded from v0.5.4 or earlier may keep the old setting until the mod is reinstalled or updated with StarPocket Client (v1.1.2 or later); see below).
   - You can change it later in settings, under "PocketRoles → Chat translation".
   - A fresh install defaults to "off" from v0.5.5 on (`Bind("Translate", "Enabled", false)`). But **for someone who upgraded from v0.5.4 or earlier and never changed this setting, the config file may still say "on".** BepInEx does not overwrite a value that is already in the file, and the file cannot tell "on because the host chose it" from "on because that was the default". v0.5.5 tells that host once and hands them `/opt upgrade off`, but changes no value. When the mod is reinstalled or updated with StarPocket Client (v1.1.2 or later), the answer given on the first screen is written to the config file.
- **How automatic reporting is decided**

   - On the first screen of StarPocket Client, under chat translation, there is an "Automatically report players removed for cheating to Innersloth" checkbox. It starts unticked. Only if you tick it does anyone get reported automatically (players are still removed from the room either way, as before).
   - You can change it later in settings, under "PocketRoles → Aegis".
   - However PocketRoles is installed, the default in v0.5.5 is "off" (`Bind("AntiCheat", "AutoReport", false)`). The key `[AntiCheat] AutoReport` existed in no build up to v0.5.4, so an upgrader's config file does not hold it either: every way of installing starts off.
- People who join have not agreed to anything. When translation is on, the welcome line says "Auto-translation is on" (not if the host changed or turned off the welcome line).

### 3.10 What the Software keeps on the host's PC

| Record | Contents | When it goes away |
|---|---|---|
| Restriction list `aegis-bans.json` | Name, hash, level, end date, rule, evidence number, erase code | Reduced 30 days after it is no longer needed. Kept while a restriction lasts (kept for good if it has no end). Only the count lasts 1 year after the last restriction ends |
| Date of a lift on appeal (in the same `aegis-bans.json`; from v0.5.5) | The date the lift list lifted it and, when that PC holds a restriction the author checked, the name of its rule (for example KillRole) | Until that PC's restriction record goes (up to about 1 year). The rule name until the 30 days after the lift are over. During those 30 days it is used so that Aegis does not restrict that person automatically. If Aegis removes them from a room during those 30 days, the evidence record notes that it was within 30 days of a lift, and when the 30 days end |
| Evidence records `evidence\AEG-….json` | Name, hashes (of the PUID and of the friend code), erase code, time, room, server region, rule, numbers, 20 log lines about that player (for a callout notice, the first 40 characters of the player's own line). No IP addresses or other people's chat | 90 days (for everyone, restricted or not, because appeals can arrive late). Records a restriction still in effect points to: 90 days after they were made or 30 days after the restriction ends, whichever is later (kept for good if it has no end). With an erase request (Article 12), deleted without waiting for the 90 days (for ones newer than 30 days, the name, room code, log lines and detection text go at once and the rest after 30 days) |
| Lines backing an evidence record `evidence\AEG-….log` | Only 2 lines of the game log from when the record was made (the Aegis detection line and the line where the mod wrote the record; they hold a name, what was detected and the first 8 characters of the PUID hash). The author's ban console uses them to check that the record matches the log. No other lines are copied | Copied before the game log is deleted at 30 days; deleted together with the evidence record (at once with an erase request) |
| Host's lists `Banlist.txt` and others | PUID (or friend code if none) and name | Never deleted automatically (lists the host made) |
| Game logs | Names, room codes, join and leave times, Aegis lines, first 8 characters of PUID hashes, the game server's address | 30 days (only the 2 lines backing an evidence record stay with it, as `evidence\AEG-….log` above) |
| Report zips and "One player's evidence" zips on the desktop (3.3) | As in 3.3 | 30 days (the launcher deletes them) |
| Launcher record `launcher.log` | Launcher actions | When it is over 1 MB, deleted the next time the launcher opens (no limit in days). The same in the Client |
| Recent people and rooms `recent.json` (Client; planned) | Names, times, room codes | 30 days |
| Consent record `consent.json` (Client) | Versions of the terms and policy agreed to, time, the chat translation and automatic reporting checkboxes | Deleted on uninstall of the Client. The file itself is not sent; Client sign-in sends only the agreed version (planned; 3.2) |
| Sign-in token `account.dat` (Client; planned) | StarPocket Account token, protected by Windows for this user on this PC | On sign-out, expiry or uninstall. If sign-out cannot be sent while offline, the token is kept until it can be sent, for at most 7 days |
| Friends cache `friends.dat` (Client; planned) | Cached friends list, blocks and private nicknames, protected in the same way | When you press "Delete Friends data", or uninstall |
| Avatar cache `cache\avatars\` (Client; planned) | Copies of friends' avatar images | On removal or blocking, after 30 days without viewing, or on uninstall |
| Room details `room.json` (PocketRoles; planned for the next MOD version) | Your hosted room's map, players, capacity, state, public/private, region and code; no names. Updated every 5 seconds while hosting | On leaving the room or ending the game |
| This PC's account status `access-status.txt` (planned) | Whether there is a shared ban, the end date, the reason and article, the first 16 characters of the shared-ban line, the erase code (the person's own; only during a ban), the version of the definitions file used, and when it was checked. Short marks that tell which account a line is for (the first 16 characters of a hash of the Among Us account and of the Steam account number, mixed with a random value that exists only on this PC; they are only used inside this PC. Steam account numbers are short, so by trying, one can find which number a mark belongs to). No names, PUIDs or friend codes. Never sent anywhere, and not put in report zips. The status, end date, reason and code are stored in a form that can't be read just by opening the file in Notepad or similar (encrypted). But someone who uses the same Windows user may be able to read another person's line with some effort, by using that person's Steam account number | Rewritten when the status changes, and once a day while signed in. Up to 4 accounts. The line of an account that has not signed in with PocketRoles for 30 days is deleted, even during a ban or a ban with no end. Deleted on uninstall |
| Aegis events `events.log` | Names, detections | 30 days |
| Last Discord post `discord-last.txt` | Message number, room code | Deleted when the room closes, and at the next room or the next start (it stays only if the webhook is turned off after the game crashed) |

- If `[Diagnostics] WireLog` (off by default) is turned on, raw network content goes into the log. Turn it on only while investigating.
- The host handles the records on the host's PC. We cannot see records on other hosts' PCs.

### 3.11 Aegis scans

1. Aegis checks only these things on the host's PC:

   - names and contents (hashes) of files in the Among Us and mod folders
   - the **names** and the **program files** of running apps (from v0.5.5)

      - To find renamed cheats, the program file (.exe) is checked too. What is checked: a mark made from its contents (SHA-256), the product information written in the file, and the name of whoever signed it. These are compared with the list of tools that can be used for cheating, inside this PC only.
      - Whether something is Microsoft's is decided by whether its certificate chains to a Microsoft root, not by the name of the signer.
      - Only the exe's location is asked of Windows. An app's memory, windows and screen are never looked at.
      - Only apps in your own Windows session are checked (not Windows services, and not other users' apps).
      - The result is not sent anywhere.
   - names of loaded drivers
   - Windows protection settings (Secure Boot, TPM, test mode and so on)
   - Inside the game (PocketRoles' self-check), it also checks the names, locations and signatures of DLLs loaded into the game (compared with cheat names; names of ones loaded from Windows folders are compared too), and whether the game's code (GameAssembly.dll in memory) has been changed. It only looks inside the game itself.
   - PocketRoles in the game (planned) turns the PUID and friend code of the Among Us account signed in into hashes of the same form as the shared-ban list, and compares them with the list. The PUID and friend code are only used in memory; they are not written down or sent.
   - PocketRoles in the game, the tray Aegis, today's launcher and StarPocket Client (all planned) will read the number of the Steam account signed in now from Windows settings (the registry), turn it into the short mark in 3.10 and the key that opens the line, and use it only to tell which line of `access-status.txt` to use. This is so that, on a PC shared with family, the screens don't show another person's ban or code. The number itself is not written down or sent.
2. It does not look at photos, documents, other apps' screens, key presses, browsers or passwords.
3. Results of the PC scan are never sent anywhere.

   - Even when you press "Contact support" on the "You can't play yet" screen, only the code, app version and language are filled in at first. Names of what was found are not added (you can write them yourself if you want).
   - Reporting a player removed for a "Certain" cheat to Innersloth (3.9) is a decision about what happened in the room, not a result of the PC scan.
   - The result of checking your own shared ban is not sent anywhere either (3.9).
4. When scans happen

   - Today's launcher: when it opens; every 30 seconds while the tray Aegis is running (app names and program files; newly started apps are noticed every 2 seconds; it only notifies and writes to `events.log`, it never stops anything); and when Play is pressed
   - StarPocket Client (planned): when it opens, when game files change, when a newer definitions file arrives, every 30 seconds while the window is visible (app names and program files), when "Check again" is pressed, and when Play is pressed
   - PocketRoles (in the game): at start, every 60 seconds, and when menus open (only inside the game itself)
   - Checking your own account (PocketRoles; planned): while signed in, it looks every 2 seconds for a change of account. It compares again when the account changes, when a newer definitions file arrives, when a ban ends, and before a room is created
5. Aegis installs no driver. It does not restart the PC or change settings.

### 3.12 StarPocket Friends (planned)

This feature is for people signed in to StarPocket Client (Terms Article 8(10)). It is not in today's Client. A Client that is not signed in does none of the communication or storage described here.

| Information | How we get it | Why | How long |
|---|---|---|---|
| Friends settings (show online status, share room code, accept friend cards) | You choose; online status is on by default for every eligible age, visible only to approved friends; room sharing is off by default | To show only what your settings allow | Until changed or the account is deleted |
| Friend connections (who is connected and since when) | When a friend card is approved | To show your friends | Until removed or the account is deleted |
| Friend-card fingerprint (an irreversible form, not the card itself) | When the card is created | To identify the card owner when used | Until used; expires in 20 minutes and its record is deleted within 1 hour of expiry |
| Pending requests (from whom to whom) | When a card is used | To let you approve or decline | Until approved or declined; at most 7 days |
| Blocks (who blocked whom) | When you block someone | To hide you from them and prevent use of their card; they are not told | Until unblocked or the account is deleted |
| Marks of people you declined or removed | When you decline or remove someone | To prevent use of that person's card for 30 days | 30 days |
| Counts (cards created or used, requests and so on, not their recipients) | Automatic | To prevent repeated actions | 7 days |
| Reports (reason, reporter and reported person, reported item such as avatar version) | When you press "Report" | Only the author reviews them and decides to hide, limit or remove content; never sent to AI | 90 days after the author marks them reviewed; counts only for 1 year |
| Avatar (supplied picture number, or a 256×256 PNG without original location or device metadata, and fingerprint) | Selected or uploaded in the Client (3.2) | To show approved friends and prevent reupload of removed images | Until replaced, removed or the account is deleted; removed-image fingerprints for 1 year |
| Online status (Client open or playing PocketRoles), shared room details and code, count of people pressing "Join" | The signed-in Client sends updates every 30-120 seconds (3.9) | To show friends your current status and room | **Not stored on disk**; server memory only. Room details and code are removed after 90 seconds without updates. Both status and room information disappear when the server stops. Only a date is kept for last activity (the token's last-use date; 3.2) |

- Only friends you approved see your display name, avatar, status and shared room. There is no public URL or name search.
- We cannot prevent people who see your status from recording it on their PC or in screenshots. Add only people you really know.
- Uploaded images go only to approved friends in the same age band. Photos showing a real person's face, including your own, are not allowed. If reports accumulate quickly, the image is hidden automatically before human review, and the author checks it (Terms Article 13(1)). Fingerprints of images the author removes are kept for 1 year to block reupload. No AI inspects image content.
- People under 13 cannot use Friends. Ages 13-15 need confirmation through a parent's or guardian's email, cannot upload images and see supplied pictures instead of friends' uploads. Ages 16-17 check "I will use this with my parent or guardian" and may upload images, shown only to friends aged 16-17. Images from people aged 18 or over are shown only to friends aged 18 or over. Friends in a different band see a supplied picture instead.
- In a friend's room you see each other's in-game names through Among Us itself. StarPocket Friends does not use, send or receive Among Us's own friends, friend codes, PUIDs or in-game names, and does not contact Innersloth or send it Friends information.
- What the Client sends and keeps on the PC is in 3.9 and 3.10; retention is summarized in Article 5 and deletion in Article 12.

### 3.13 Applications to "Join us" (contributor recruitment)

These are applications to the recruitment of contributors who help as hosts, with support, with translation or with development (Terms of Use Article 6(4)). This is separate from the Steam-copy program (3.1). You apply by signing in to the support site (https://support.starpocketgames.com) with email or Discord. Every role requires applicants to be 16 or older, and applicants aged 16 or 17 need a parent or guardian's consent. Support members, who read the content of support requests, must live in Japan. Host, translation and development contributors are not shown support requests. Helping is unpaid; no money is paid. If we decide on a non-cash thank-you, we will tell you.

| Information | How we get it | Why |
|---|---|---|
| Roles you want (host, support, translation, development), languages you speak, age band (16–17 / 18 or older), when you are available (up to 100 characters), a few words from you (up to 400 characters), interview method (text or call) | You choose or write them in the application | To see what you can help with, and to select and interview |
| The "I have my parent or guardian's consent" tick | Ticked by people who chose 16–17 | To go on to checking a parent or guardian's consent before approval |
| Whether you live in Japan | Ticked by people who chose support (we do not ask for an address) | To check the condition for support |
| GitHub username | Only if someone who chose translation or development writes it (not kept if only other roles were chosen) | Selection for translation and development, and contact while helping |
| Quiz result (that every answer was correct, and how many times you answered again after a wrong answer) | Answered before applying. You cannot send until every answer is correct. The answers themselves are not kept | To help you learn the rules. The count is only a reference and never decides selection on its own |
| How you signed in to apply (email or Discord), the ID used for matching (the account's email address or Discord ID), the account's display name, the language of the application page | Kept automatically from the signed-in account (the display name is only the account's name shown on the author's screen) | To check at approval that it is the same account and sign-in method as when you applied. To tell on the author's screen whose application it is. To choose the language of replies. The author also views display names, sign-in methods and last sign-in dates in the add-staff list for selection and registration (3.2) |
| Receipt number (JN-…), when you applied, status (new, invited to interview, interviewed, approved, not selected, withdrawn) and when it was decided, the replies on the application | Made by the application and replies | To contact you about selection. So you can read them under "Your requests" |
| The author's notes (including interview notes; you cannot see them) | Written by the author | Selection |
| The date a parent or guardian's consent was confirmed | Written by the author when approving someone aged 16 or 17 | A record that the condition for approval was checked |
| Approval date, removal date, the date you agreed to the contributor pledge and the pledge version | Written by the author | Registering you as a contributor or support member, and a record that you agreed to the pledge |
| Whether you agreed to translation of the application, and the version of this policy you agreed to | You agree on the application page (planned) | To decide whether the author may translate it |
| The account's email address (if there is one) | From the account | To email you when the application is received, and possibly when we reply (through Resend, an email-sending company; the emails contain no application content, answers or your name). To keep receipt emails to the same address to 3 a day, the address is turned into a mark that cannot be turned back and kept for 1 day |
| A mark made from your IP address | Made automatically when you apply | To limit applications from the same place to 20 a day. The IP address is mixed with a password that changes every time the server starts, turned into a mark that cannot be turned back (made the same way as in 3.1 item 9), and kept in the database with the application for 1 day only. The IP address itself is not kept |
| The number of wrong quiz answers if you stopped without applying | Automatic | Carried over as the retry count when you next apply |

- **Who reads it**: only the author reads applications, your replies and the author's notes. Admins and support members are not shown them. This is a separate channel from the 9 support teams (Article 2(4)).
- **Notices to the author**: when an application or a reply arrives, only a fixed message such as "There is an application" and the URL of the admin page are sent to the author's Discord DMs. The receipt number, content and names are not sent.
- **Discord notices to you**: if you chose "Also tell me by Discord DM when there is a reply" on your account page, replies to your application also trigger a DM under the same rules as 3.1 item 7. It contains only fixed text, the receipt number, a link to read it and how to stop; no application content or reply text. Nothing is sent if you did not choose it.
- **Selection**: the author reads the application, decides whether to invite you to an interview or not to go ahead, and decides after the interview whether to approve it. The quiz result alone never decides it. Applications are not used for AI reply drafts (3.1, item 11).
- **Interviews**: text interviews take place in the reply box of your application. If you choose a call, the interview is a Discord voice call. It is not recorded. We do not ask for your real name, address, school or a photo of your face. To confirm it is you, we may ask you to write your receipt number and a phrase given in the interview as a reply to your application. We never ask for passwords or sign-in codes.
- **If you are 16 or 17**: before approval we check a parent or guardian's consent. Ask them to read what is involved and give consent, then tell us in a reply to your application, and we record the date we confirmed it. We do not ask for the parent's or guardian's real name or address.
- **After approval**: the author sends the contributor pledge. The pledge has 16 items, and the current version is 2026-10-04. The full pledge is also on the pledge page of the support site (https://support.starpocketgames.com/en/join/pledge). When you reply "I agree", we record the date you agreed and the pledge version (Terms of Use Article 6(4)). Support members also agree to the staff pledge. People approved for support read support requests as support members within the limits in Article 2(4) and Article 11. Before reading any, they first agree to the staff pledge and complete two-step verification with an authenticator app (Article 11).
- **Translation**: the author reads in Japanese. To read applications written in other languages, and to write replies in the applicant's language, only the author may translate the application text, your replies and the author's reply drafts with DeepL API Pro from DeepL SE (Germany). We ask you to agree to this on the application page, and keep the version of this policy you agreed to with the application. Applications with no agreed version kept (those sent before this is added to the application page) are not translated. It will be added to the application page (planned). Hiding parts that look like contact details or numbers before sending, not keeping translations, the records kept, and how DeepL handles the text are the same as in 3.1, item 12.
- **How long we keep it** (also summarised in Article 5)

   - An application with no decision yet is withdrawn automatically 90 days after the last activity (the application, a change of its status, or a reply from either side). The author's notes and drafts do not extend this.
   - Applications that are not selected or are withdrawn (including automatic withdrawal) are deleted 90 days after the decision.
   - The applications and records of approved people are kept while they are contributors or support members and for 1 year after they stop.
   - The number of wrong quiz answers if you stopped without applying is deleted 90 days after your last answer.
   - While the author has decided to keep something for an investigation, such as into a leak, deletion waits.
   - After deletion, it stays in server backups (the last row of Article 5) for the backup period.
- **Applying again**: after an application is not selected or is withdrawn (including automatic withdrawal), you cannot apply again for 30 days. While you have an application in progress, and while you are a contributor or support member, you cannot send a new application.
- **To withdraw**: if you want to withdraw your application or stop being a contributor or support member, sign in and reply to your application.
- We use this information only for selection, contact, registering contributors and support members, the record of agreeing to the pledge, and limiting the number of applications. We do not sell it or give it to others (Article 7).

## Article 4 (Summary of purposes)

We use information about individuals only for:

1. answering support requests;
2. deciding and reviewing Aegis decisions, room restrictions, shared bans and appeals correctly;
3. erasing records and handling other requests (Article 12);
4. running accounts and keeping sign-in safe;
5. fixing bugs and improving the Software and Services (when improving the FAQ, in a form where no individual can be identified);
6. preventing abuse, attacks and impersonation;
7. telling people about changes to the terms and this policy;
8. doing what the law requires.
9. showing the people you choose what you choose to share through StarPocket Friends (3.12; planned).
10. selecting and contacting applicants to contributor recruitment ("Join us"), and registering contributors and support members (3.13). Display names, sign-in methods and last sign-in dates viewed by the author in the add-staff list are used only for selecting and registering these applicants (3.2). Non-applicants' information is not used for this list.

## Article 5 (How long we keep things)

| Information | How long |
|---|---|
| Ordinary support requests (contact page, receipt-number page and support chat, including live chats; applications in 3.13 have separate rows below) | 30 days after being marked resolved. If you write nothing for 7 days after our reply, the request is marked resolved automatically (planned). Requests waiting for a reply are kept until we reply and they are resolved. You can delete a request at any time with "Delete this request" on the receipt-number page (or under "Your requests" when signed in) (planned; it is deleted after 24 hours and you can undo it until then). Until that button exists, ask through Article 14 and we delete it. Records attached using "Attach the conversation so far" stay for this request retention period (initially ticked; you can untick it). Cancelling in the waiting room does not delete the request; it is deleted 30 days after being marked resolved. For chats not handed to a person, see the chat row below. After deletion, it stays in the server backups (the last row of this table) for as long as they are kept |
| Friend code in the appeal form's dedicated field | Until matching is done, at most 7 days. Chat does not accept appeals and directs you to the appeal form (3.1 item 6) |
| Friend codes sent by email or in a Discord ticket | Deleted with that email or ticket (email: 90 days after the last message (planned; not deleted automatically yet); tickets: when decided). The copies the import tool saves on the author's PC are not deleted automatically yet either (planned) |
| Email address in a support request (if written) | Deleted with that request |
| A mark made from the reply-to email address (3.1 item 9; planned) | 1 day (deleted from the database; stays in backups for as long as they are kept) |
| Other people's names, account names and URLs written in a report of a fake download site | 30 days after we have checked it and finished dealing with it. Deleted together with that request |
| Agreement versions and times, age band (including answer date, lowest prior band and its year; planned), parental confirmation date (planned) | Deleted with the request or account. Chat Terms, Policy and explanation versions and agreement times are deleted 24 hours after the start if not handed to a person, or with the request if handed over. Unknown versions in older records are not filled in. Backups: last row of this table |
| Account | Until you delete it. If you do not sign in for 2 years, deleted after a notice 30 days before (planned; today nothing is deleted automatically) |
| Records of limits on the Services (the code, article, end date and author's note under Terms Article 13) and of an account suspension (time, reason, note) | No expiry (today nothing is deleted automatically) |
| Account sign-in records (site) | 90 days. Client sign-in records (planned; dates only) are deleted with the token |
| Device marks, the list of signed-in devices (planned) | 1 year after last use, or when the account is deleted |
| Client sign-in token (3.2; planned) | Expires 90 days after sign-in; continued use does not extend it. Its server record is deleted 1 day after expiry, sign-out or revocation |
| Exchange token for Client Discord sign-in (3.2; planned) | 60 seconds; record deleted within 1 hour after expiry |
| Parent's or guardian's confirmation link and email (ages 13-15; Article 10; planned) | 24 hours; record deleted on confirmation or expiry, keeping only the confirmation date |
| StarPocket Friends connections, blocks, settings and avatar (3.12; planned) | Until removed, unblocked, changed or deleted; deleted with the account |
| Friend-card fingerprint, pending requests, declined/removed-person marks and counts (3.12; planned) | Card 20 minutes (record deleted within 1 hour of expiry); pending requests 7 days; declined/removed marks 30 days; counts 7 days |
| Friends reports (3.12; planned) | 90 days after the author marks reviewed; counts only 1 year |
| Removed-avatar image fingerprints (3.12; planned) | 1 year |
| Online status, shared room, count of people pressing "Join" (3.12; planned) | No disk storage; server memory only; rooms disappear after 90 seconds without updates, and status and rooms disappear when the server stops |
| Verification codes (the 6 digits sent by email) | 10 minutes (stop working once used). The record of a used or expired code (with the email address and the IP mark) is deleted 24 hours after it was made |
| Email address change (the old and the new address) (planned) | 30 days after it finishes |
| Operation records (sign-in and out, sending a request, chat handover, cancelling the waiting room, choosing to wait for a reply, timeout or absence transitions, the author's acceptance, replies, ending, transfers and availability changes, connecting Discord and choosing or stopping notifications, and other staff actions; account number, time and request link; no message text or IP address) | 30 days. Records of non-author staff opening, replying, changing status or adding appeal assessments, and adding, removing or changing staff: 1 year |
| Assessments added by staff to room restriction appeals (who wrote them and when) | Deleted with the appeal: 30 days after resolution |
| Staff request-view and reveal records (who, when and which request) | 1 year |
| Your choice "Also tell me by Discord DM when there's a reply" (3.1 item 7, 3.2) | The account choice: until you turn it off or delete the account. The choice for a single request: deleted with that request |
| Records of Discord DM notices and attempts (in progress, sent, not delivered, delivery unknown, not sent under the rules; times including the sending start, attempt number, which reply, request and account numbers, result and reason code; no Discord ID and no DM text) | 90 days |
| Support members' replies waiting for approval (a support member's reply text before the author checks it; Article 11) | Until the author posts or returns it. 30 days after it is posted or returned (not deleted while held for a leak investigation) |
| Holds for leak investigations (a mark that keeps, instead of deleting, the requests a staff member recently opened, revealed or replied to when that member is paused or removed; Article 13) | 90 days (extended if the investigation continues). While held, those requests and replies are not deleted either |
| The "may be sent to DeepL for translation" tick, with the version and time (3.1, item 12; planned) | Deleted with that support request |
| Translation records (who translated how many characters of which request; 3.1, item 12, and 3.13) | 30 days for the author's translations; 1 year for other staff members' translations |
| Each person's daily count of translated characters (who, and how many characters that day) | Until the next day; earlier days are deleted |
| Text sent to DeepL, and translations (3.1, item 12, and 3.13) | Translations are not kept on our servers. DeepL states that with the paid API Pro, text is deleted once translated. Original requests and applications follow their own retention periods |
| "Join us" applications (no decision yet; 3.13) | Withdrawn automatically 90 days after the last activity (the application, a change of its status, or a reply from either side); then as in the next row |
| "Join us" applications and the author's notes (not selected or withdrawn, including automatic withdrawal; 3.13) | 90 days after the decision, separate from ordinary support's 30 days after resolution |
| "Join us" applications, the author's notes, the date a parent or guardian's consent was confirmed, and the date and version of agreeing to the contributor pledge (approved people; 3.13) | While a contributor or support member, and for 1 year after |
| The number of wrong quiz answers if you stopped without applying (3.13) | 90 days after your last answer |
| The IP-address mark for "Join us" applications, and the email-address mark used to count receipt emails (3.13) | 1 day (deleted from the database; it stays in the server backups for as long as they are kept) |
| Staff registrations (added person's email or Discord ID, role, teams and time added) | While on staff, then deleted 1 year after removal |
| The staff pledge version and time agreed, the authenticator app key (sealed) and failed check counts, the date a parent's consent was confirmed for staff aged 16-17, the reply-approval setting and pause records (information about people who became staff) | The authenticator app key and failed check counts are deleted within an hour of removal (if the person is added again, they register again). Everything else is deleted one year after removal |
| Applications for a Steam copy (3.1) | 30 days after being marked resolved (within 7 days of the close, every request is marked resolved) |
| The mark made from the IP address (3.1, item 9) | 1 day (deleted from the database; stays in backups for as long as they are kept) |
| Email support (Gmail) | 90 days after the last message (planned; not deleted automatically yet) |
| Discord appeal tickets | Deleted when decided |
| Notices in the staff Discord | They stay in Discord; the author deletes them by hand |
| Report zips, "One player's evidence" zips (3.3) and imported records held by the author and admins | 90 days after the case ends (planned; not deleted automatically yet; 3.5, item 4) |
| Report-host record (the ban console's `reporters.txt`) | Lines of reports that were mistakes: 30 days. Lines of trusted admins: until the trust is taken back |
| Lines in the shared-ban list | Until the end date. Lines past their end date are removed within 30 days |
| Lines in the erase list | 90 days or more (so that they also reach evidence records, kept 90 days, on host PCs that have not started for a while; the newest 1000 are used) |
| Content sent to Anthropic (your masked bot messages, public articles in that language and language code, and text sent for staff drafts) | Under Anthropic's rules, within 30 days (up to 2 years for content judged to break its rules; safety scores up to 7 years). On our server, bot questions and answers stay for 24 hours from the chat's start, or with the request if attached on handover. Backups: last row of this table |
| Support chat not handed to a person (planned: questions, search results, AI answers and unavailability notices, account number, agreement versions and times) | Deleted from the database 24 hours after the start; also when the account is deleted. The author cannot read it on the management screen during that time. If attached on handover it is copied into the request; the chat-side search records are deleted immediately whether attached or not. Backups retain it for their retention period |
| AI cost ledger (reservation ID, UTC day, chat link, reserved or settled amount, settlement mark; no message text) | No time limit (no automatic deletion today). The chat link is removed when the chat is deleted. Backups: last row of this table |
| Per-person daily AI count (account number, UTC day and count; no message text) | Deleted on the next UTC day, or when the account is deleted. Backups: last row of this table |
| Chat usage count records (account number, time, start/send distinction; no message text) | 24 hours. Backups: last row of this table |
| Daily AI call and usage totals (token counts; not linked to a person) | No time limit (no automatic deletion today). Backups: last row of this table |
| Home server operation logs | 14 days |
| Records of blocked attacks (Cloudflare; they contain IP addresses) | The period set by Cloudflare's rules (kept in Cloudflare's dashboard, where the owner can see them; not kept on our servers) |
| Server backups (copies used to restore data lost through a fault or a mistake; they contain the requests, chat records, agreement versions and times, usage and AI cost records, email addresses, accounts, marks and so on as they were when the backup was taken) | Nightly backups on the home server: the newest 14 are kept and older ones are deleted automatically. Nightly backups carried to the author's PC: the newest 30 are kept and older ones are deleted automatically. These are counts, not day limits: gaps in creation or retrieval can leave backups older than 14 or 30 days. Backups taken before switching to a new version: those older than 14 days are deleted at each switch (the newest one is kept; while there is no switch nothing is deleted, so they can stay longer than 14 days). Backups the author takes by hand (and copies the author makes by hand, such as on a USB stick): currently kept until deleted by hand, with no automatic deletion. Automatic deletion after 90 days is planned; USB copies older than 90 days will be removed the next time copies are made. While the USB is disconnected, they cannot be removed and may remain longer than 90 days. Backup key-file encryption is planned: the home server keeps the public key and encrypts before sending to the author's PC; the private key and passphrase stay only on the author's PC, for restoration only (Article 11(3)) |

- How long things stay **on a host's PC** is in 3.10: Aegis evidence records (and the 2 log lines backing each) 90 days; logs, report zips, copies of sent reports and the like 30 days. These are records on the host's PC, but we set these defaults.
- **About GitHub's change history**: the central lists are managed on GitHub. Even after a line is removed from a list, the old line stays in GitHub's change history. Only hashes and codes are there; no names.

## Article 6 (Companies we use, and sending information abroad)

1. We use the services of these companies. For each company we show the company's country, the country of the servers that handle the data, the legal basis for sending it abroad, and the measures the company takes (Act on the Protection of Personal Information, Article 28; Enforcement Rules, Article 17(2); the Personal Information Protection Commission's Q&A 10-24 and 10-25).

| Company | For what | Information handled | Company's country | Servers' country | Basis (proposal) | Measures the company takes |
|---|---|---|---|---|---|---|
| Anthropic PBC | The support bot (planned), staff reply drafts and translations (off today; even then only when a staff member presses the button), reply drafts in the ban console (only when the author presses the button) | For the bot: public articles in that language, the language code, and your own text in that chat, after masking contact details and similar parts (up to the latest 20 previous messages plus the current question within the character limit). Request category, search results, previous bot answers, text written by the author and account information are not sent. Questions and answers stay on our server for 24 hours from the start, or for the request retention period if attached on handover (backups: Article 5). For drafts, the text (with friend codes, email addresses, URLs and long numbers hidden), kind, language and code of that support request (3.1, item 11). For the ban console's drafts, the reply's kind, language, ban number, rule category, dates and template (names and a shared ban line's number as placeholders); the box for pasting request text is not shown until the basis is settled (3.5, item 5) | United States | Storage in the United States. Under Anthropic's rules, the answer may be computed in a region outside the United States | Consent (a separate optional AI tick on the chat's first screen), or a system meeting the standards (DPA). Email and Discord requests are not sent until the basis is settled | A data processing agreement (DPA) is part of its terms. API content is not used for training. Deleted within 30 days (up to 2 years for content judged to break its rules; safety scores up to 7 years) |
| Cloudflare, Inc. | Where the official site sits, entry to the home server (hides the home IP address), bot check | Traffic content (between the encryption end points), IP addresses, browser information, including planned Client traffic to our server: sign-in, status, room details and code, avatar images (3.12) | United States | Cannot be identified (traffic is handled at whichever of its locations around the world is near the visitor) | A system meeting the standards (DPA), or consent | Publishes a DPA and security measures (being checked) |
| Resend (email-sending company) | Emails with the verification code, receipt notices (planned), reply notices, the notice when Discord is connected, and account notices (email address change, new device or new Client sign-in; planned), parental confirmation emails for ages 13-15 (planned), and notices that a "Join us" application was received and that a reply has arrived (3.13) | Email address, email content (no support message text; "Join us" notices contain no application content, answers or your name; receipt and reply notices contain the receipt number; receipt and reply notices include the team name, and reply notices also include the handler's name (a staff display name) (planned); new-Client notices include the app name and time (planned); parental confirmation emails go to the parent's or guardian's address and contain the confirmation link (planned); the notice when Discord is connected contains the display name of the connected Discord and the time), the logo image (our icon as a PNG embedded in the email; the handler's icon and stamp on reply notices (planned) are pictures we made, embedded the same way; nothing is loaded from outside; whether open tracking (knowing whether an email was opened) is switched off in Resend's settings is being checked) | United States | United States | A system meeting the standards (DPA; being checked) | A data processing agreement (DPA; being checked). Its published privacy policy and security measures |
| Discord Inc. | Community, tickets, notices to staff, sign-in with Discord, including from the Client (planned), "Join us" notices to the author, interview calls chosen by applicants (3.13), and DMs saying a reply has arrived (only for people who chose "Also tell me by Discord DM when there's a reply"; 3.1 item 7) | Names and messages in Discord. Notices contain only number, kind and language. For sign-in, the Discord user ID and name. "Join us" notices contain only a fixed message and the URL of the admin page. Discord handles your voice if you choose it for an interview call; we do not record the call. For reply DMs, your Discord user ID (the recipient) and the DM text (fixed text, receipt number, link to read it, how to stop; never the reply text) | United States | Cannot be identified (being checked) | Being checked  (check separately the parts where users use Discord themselves and the parts we send). Reply DMs are sent only if the person chooses them (consent; Article 17(3)). The choice names Discord Inc. (United States) and links to this policy. If you are under 16, please decide with a parent or guardian. | Its published privacy policy. No DPA with us |
| Ticket Tool (the Discord ticket bot) | Discord tickets | What is written in tickets | Being checked | Being checked | Being checked | Set not to keep transcripts |
| GitHub, Inc. | Downloads (Releases), the definitions file, the central lists, where documents such as the terms are kept | Visitors' IP addresses, list contents | United States | United States and others (being checked) | Being checked | Its published privacy policy and security measures (being checked) |
| Google LLC (Gmail) | Support email | Email content | United States | Cannot be identified (being checked) | A personal account with no DPA, so rely on consent or move to a service with a DPA | Its published privacy policy |
| DeepL SE | Translation of Steam-copy applications (3.1; paid DeepL Pro, only in the 30-day window). Translating support requests and "Join us" applications (3.1 item 12 and 3.13; paid DeepL API Pro; only when the author or a staff member who can read that request presses "Translate"; only for people who agreed to translation) | Steam application text with receipt numbers, Steam names and friend codes removed. For support requests and applications, the text to translate (your text or a reply draft written by staff, with parts that look like contact details or numbers hidden) and the languages | Germany | Germany / EU | Consent (for the Steam copy, the guide page; for support requests, the tick when sending (planned); for applications, the application page (planned)). Germany (EU) is recognised as protecting personal information at a level similar to Japan (item 3(4)) | States that with the paid DeepL Pro and API Pro, text is deleted once translated and not used for training or to improve its services. We do not keep translations |

2. Where things sent directly from the host's PC (3.9) go

   - Google LLC (United States) and DeepL SE (Germany): translation
   - Innersloth LLC (United States): official reports
   - Discord Inc. (United States): the count in the Client's community panel (planned)
   - These are sent by the host from their own PC.
3. **About sending information abroad** (Act on the Protection of Personal Information, Article 28)

   1. When we have a foreign company handle your information, for each company we rely on one of the following (the "Basis" column in the table in item 1):

      - your consent
      - a system meeting the standards: an agreement with the company (such as a DPA), or APEC CBPR certification (Enforcement Rules, Article 16)
      - only renting servers in a form where the company does not handle the data (the cloud exception; Q&A 7-53)
   2. When we rely on consent, before you start we show the following on screen and ask for your consent (Enforcement Rules, Article 17(2)): For the support-chat bot, the consent screen shows the destination country and company, what is sent and how it is masked; the country's system, company measures and retention periods are explained in this Policy linked from that screen.

      - the country and the company it is sent to
      - that country's personal information system
      - the measures that company takes (including any points where they fall short of the OECD's 8 principles)
   3. When we rely on a system meeting the standards, we tell you on request what that system is and how we keep checking it (Article 28(3)).
   4. Countries' systems

      - The United States has no national personal information law like Japan's. There are state laws (such as California's). For details, see the Personal Information Protection Commission's research page: https://www.ppc.go.jp/enforcement/infoprovision/laws/offshore_report_america/
      - Germany (EU) is recognised by Japan as protecting personal information at a level similar to Japan.
      - Where the servers' country cannot be identified, the table in item 1 says so and why.
4. The home server is in Japan (its location is not published).

## Article 7 (Giving information to others)

1. We do not sell information about individuals.
2. We do not give your information to others without your consent, except:

   1. when the law requires it;
   2. when it is needed to protect someone's life, body or property and it is hard to get the person's consent;
   3. when we need to cooperate with the work of national or local government;
   4. when the running of the Services is handed over to another person or group (Terms of Use Article 27; Act on the Protection of Personal Information Article 27(5)(ii)). We tell you at least 30 days before the handover date, and anyone who wants to leave can delete their information before that date (Terms of Use Article 27(3) and 27(5)).
   5. when you use StarPocket Friends (3.12; planned) to show approved friends your display name, avatar (including your uploaded image), online status and selected room details and code. You choose the recipients and settings; age limits also apply.
3. Publishing the central lists is described in Article 8.
4. Official reports to Innersloth are sent by PocketRoles on the host's PC (3.9). They are not sent from us.

## Article 8 (Lists we publish)

1. The shared-ban list, the erase list and the lift list are put in the definitions file and can be read by anyone on GitHub. This is so they reach every host's PC.
2. Only these are on them:

   - Shared-ban list: PUID hash (a value that cannot be turned back into the original number), level, end date, short reason word
   - Erase list: erase codes and dates
   - Lift list: erase codes, and the evidence number and date of the record we checked
   - Names, friend codes and IP addresses are not on them.
3. Even so, please note:

   1. Hosts using PocketRoles can make the PUID hash of people who join. So a host can find out whether someone who joined is on the shared-ban list. This is how shared bans work.
   2. Anyone who knows your code can see that the code is on the erase list.
   3. If you type `/cmd id` in an unregistered room, everyone in the room sees the code. If you can, type it in a registered room.
   4. The lift list holds only erase codes and evidence numbers; no names or friend codes. However, the code is made from your PUID, so the host of any room you have joined (and, if you typed `/cmd id` in an unregistered room, the people who were there) can find out that you are on the lift list, that is, that you once had an automatic restriction. The list also stays in GitHub's history (Article 5). If you do not want to be listed, say so when you appeal. In that case, restrictions in other hosts' rooms are not lifted.
   5. PocketRoles also uses the same list to check whether your own account signed in on that PC is on it (planned; only inside that PC; 3.11).
4. The author can sometimes connect a value on a list with a person, through support messages or report zips. So publishing the lists may count as "giving information to others" under the law. The lists are already published. Whether this applies has not been settled yet. We will check it, and if it applies, we will change how the lists are published and update this document.

## Article 9 (For people who only join a room)

1. We install nothing and check nothing on your PC or phone. You have not agreed to anything.
2. The host's PC may keep the following (3.10):

   - name, device type, room code, join and leave times
   - Aegis detection numbers
   - an evidence record, if you were removed (90 days; 3.10)
3. PUIDs are kept as hashes that cannot be turned back. A friend code's hash can sometimes be turned back by trying them all, so it is treated like the friend code itself (how long it is kept is in 3.10). The exception is lists the host writes by hand (`Banlist.txt` and others), which hold the PUID or friend code as it is.
4. IP addresses are not kept. In rooms on the official servers, even the host does not receive your IP address.
5. In rooms where chat translation is on, your chat text is sent to Google or DeepL for translation. The welcome line says "Auto-translation is on" (not if the host changed or turned off the welcome line).
6. If you are removed for a "Certain" cheat:

   - an official report is sent to Innersloth (unless the host turned automatic reporting off);
   - everyone in the room sees your name and the reason.
   - during a match in an unregistered room, if the cheat changed the match (for example, a kill landed), the match may be ended for everyone (at most once per match and 3 times an hour; Play Rules, Article 8(2)(4)). Aegis's message to everyone then does not show your name (Among Us's own notices may still show it). No new information is collected or sent to stop the match.
7. If the host of the room you are in gets a shared ban while the room is open, that host's PocketRoles leaves the room (planned; not in today's PocketRoles yet; Play Rules, Article 9(6)).

   - When no match is being played, it leaves at once. During a match, if the reason is one of the certain cheat detections (killrole, ventrole, abilityrole or taskimpostor in Play Rules, Article 9(5)), it leaves at once even mid-match; for any other reason (cheat, chat, harass, name, other: cheating the author decided from the records, chat, bothering others and so on), it leaves after that match ends. A reason word we do not know also waits for the match to end.
   - The people in the room are not told about the ban or its reason (in the lobby it only says "The host is leaving this room"). When the host leaves, Among Us's own "… left the game" notice still appears.
   - No new information is collected or sent for this.
8. To have your records erased, see Article 12. You do not need to ask the host.
9. How these records are handled is up to the host on their own PC. We have set the defaults to "delete automatically (Aegis evidence records after 90 days, logs and the like after 30 days)" and "do not keep IP addresses".
10. Innersloth's Terms of Use ask users not to collect or store other people's personal information from the game without their clear permission (section 11(c)). We will check how Aegis keeping records on host PCs relates to this.

## Article 10 (Children's information)

1. Many people who use our Services are children. So we do the following.

   1. We collect as little as possible. The contact form works without an account or email address, and replies can be read through a secret link. Support chat requires signing in with a StarPocket account.
   2. When consent is needed for information about someone under 16, we get it from a parent or guardian.
   3. People under 13 cannot use Discord. Please use email, the contact form or support chat (planned; chat requires signing in with a StarPocket account).
   4. Support chat (planned) uses the age band answered on the account page (3.2), without asking for a birth year in chat. If it is not "18 or over", including unanswered ages:

      - the screen asks people under 18 to use it with a parent or guardian. It also shows this to people who have not answered. Confirming this guidance is not a condition for using chat;
      - article search and the author answer, without the bot. Requests sent from chat are not used for staff AI drafts regardless of age;
      - chat has no email-address field, regardless of age.
   5. People under 18 must make accounts with a parent or guardian (Terms Article 5(1)). The account-creation confirmation screen is planned and not yet present; the account age field and an optional "I will use this with my parent or guardian" tick for people under 18 are not yet in production. They are planned to be introduced together with support chat.  You choose a birth year on the account page to determine bot eligibility and before using StarPocket Friends (planned); only the band is kept. Under 13 cannot use Friends. Ages 13–15 cannot upload images and see supplied pictures instead of friends' uploads; images uploaded by ages 16–17 are visible only to friends aged 16–17 (all planned).
   6. Parents and guardians can ask to check or delete their child's support requests, account and records (Article 12).
   7. Where something still relies on consent for someone under 16 (such as sending information abroad), we take steps to check with a parent or guardian (for example, a confirmation link sent to the parent's or guardian's email; once confirmed, that email address is deleted). As far as we can, we arrange things on bases that do not rely on consent (such as a system meeting the standards in Article 6(3)), so that a child's consent is not needed. For StarPocket Friends, ages 13-15 must complete this check before use: a one-use link to the parent's or guardian's email expires after 24 hours; the email is deleted on confirmation or expiry and only the confirmation date remains. For StarPocket Friends, ages 16-17 only check that they will use it with a parent or guardian (planned). "Join us" applications are covered separately in item 8. Reply DMs on Discord (3.1 item 7) are sent only if you choose them. The choice asks people under 16 to decide with a parent or guardian (age is not asked).
   8. Only people aged 16 or older can apply to the contributor recruitment ("Join us"; 3.13). Applicants aged 16 or 17 need a parent or guardian's consent. Before approval, the author confirms through the applicant's reply that a parent or guardian has read what is involved and agreed, and records the date. We do not ask for the parent's or guardian's real name or address.
2. People under 16 are expected to become able to ask us to stop using their information even when no law was broken (a 2026 amendment of the law, expected to start by 2028). When it starts, we will update this article.
3. Under Chinese law, information about children under 14 is especially sensitive information.
4. The same amendment is expected to require that minors' information be handled putting what is best for them (their best interests) first (Article 40-2). We handle it that way from now on.

## Article 11 (What we do to keep information safe)

1. **Rules and structure**

   - A responsible person is set (the author).
   - The author, designated admins and support members chosen by the author can read ordinary support requests (Article 2(4)). Only the author reads "Join us" applications and the author's notes; admins do not see them (3.13). Support members read only their assigned teams' requests, excluding Steam-copy and author-only "Join us" applications. Room restriction appeals are shown only if they say the restriction was a mistake, only to staff assigned to the Room Access Team (including admins), and only if they meet the time and viewing notice version conditions in Article 2(4). An unset, empty or invalid access start time, or an old, missing or unknown version, means author-only access. Requests to erase records and reports about a host are read only by the author. Staff can write only assessments and reply drafts; only the author posts replies and changes status or language. The author's notes are not shown to staff. Support members see the signed-in sender's display name for greetings, but not reply email addresses or account IDs, and cannot access room, user or restriction management screens. These limits apply at every server entry point, not only on screen. However, the method for keeping Steam-copy applications from support members is still undecided, as stated above. Requests received from support chat and live conversations are read only by the author today, never by admins or support members.
   - We record which staff member did what and when.
   - Email addresses, phone numbers, URLs, IP addresses, account numbers and friend codes in message text will initially be masked; only a handler allowed to read the request can press "Reveal". The dedicated reply-address and account-ID fields remain hidden from support members. We record who revealed information, when and for which request.
   - For staff other than the author, the staff member's name and the current time are watermarked throughout the management pages. We record who opened which request and when; unusually many views in a short time notify the author and stop that member's viewing. View and reveal records are kept for 1 year.
   - Support members have no export, bulk-copy or print view. Screenshots and screen recordings cannot be completely prevented. Article 13 applies if information leaks.
2. **People**

   - Admins promise confidentiality.
   - We explain information handling to admins.
   - Staff other than the author will agree to confidentiality on the staff agreement screen. At first, all support replies will wait for the author's approval. The author may turn this setting off for individual members, but replies containing apparent private contact details or similar content will always require approval. A staff member does not see requests received before they were added. These three measures and the two-step verification below are in place together with staff and teams, as the four staff safeguards.
3. **Physical**

   - The home server is inside a home.
   - Disk encryption and TPM key protection on the home server have not been checked on the actual machine. Current backups are ZIPs without key-file encryption. We will encrypt server backups with a key file (planned; Article 5): the public key stays on the home server, which encrypts backups before sending them to the author's PC. The private key and its passphrase stay only on the author's PC. Backups are decrypted only for restoration.
4. **Network**

   - Cloudflare Tunnel hides the home IP address; no home port is opened.
   - The staff page is accessible from the internet, but only listed Discord IDs or email addresses and staff added by the author can enter. Support members and admins other than the author must agree to the staff pledge and pass two-step verification with an authenticator app. Only the author adds admins and support members, one at a time (Article 2(4)), and they can enter only by the method used when added.
   - IP addresses are not written to logs.
   - URLs sent to us are not opened.
   - Email goes through an email-sending company, not directly from the home server, to keep its IP address out of mail.
5. **Foreign environments**

   - Article 6 lists companies' and servers' countries and the measures taken (United States and Germany, or the reason a country cannot be identified).
   - The home server is in Japan. Support members must also live in Japan.
6. Details that would weaken security if published are omitted.

## Article 12 (What you can ask us to do)

1. For your information that we hold, you can ask us to:

   1. tell you what we use it for
   2. show it to you (disclosure)
   3. correct it
   4. stop using it, or delete it
   5. stop giving it to others
   6. show you records of giving it to others

   - There is no charge.
2. How to ask

   1. **To erase records (the easiest)**

      1. Type `/cmd id` in the chat of a PocketRoles room. Your erase code (16 letters) appears.
      2. Send that code to the contact in Article 14 (write "Erase records" in the subject or first line). You can also choose "Please erase my records" on the support site's room restriction appeal form. This is read only by the author, never by Room Access Team staff (Article 2(4)).
      3. The author puts the code on the erase list. Each host's PC erases your records the next time it gets the definitions file (at game start, and once a day while running).
   2. **To delete an ordinary support request** (withdrawal and retention for "Join us" applications are in 3.13; you can also make the information requests in paragraph 1 of this article): press "Delete this request" on the receipt-number page (a secret link for forms; sign-in for chat) (planned; it is deleted after 24 hours and you can undo it until then). Until that button exists, ask through Article 14.
   3. **To delete an account**: press "Delete account" on the account page (planned). Until the button exists, ask through Article 14 (the contact page, category "Other") and we will delete it. After deletion, it stays in the server backups for the periods in Article 5 (manual backups currently stay until manually deleted; automatic deletion after 90 days is planned). Friends data alone (connections, blocks, settings and images; 3.12) will be deletable through "Delete Friends data" in the Client settings, leading to the account page, and is also deleted with the account (planned).
   4. **To see your information**: besides the code, we ask a few questions to confirm it is you (when you joined, what in-game name you used and so on). This is so we do not show it to someone else. For account information, sign in first and then ask. You can choose how we show it (for example, as electronic data by email).
   5. **From a parent or guardian**: ask with your child's code, a support request written together with your child, or from your child's account.
   6. **To withdraw or delete a "Join us" application**: sign in and reply to that application under "Your requests". The application is marked as withdrawn and deleted 90 days after the withdrawal (3.13, Article 5). For people approved as contributors or support members, the records are deleted 1 year after they stop.
3. What is erased (with a `/cmd id` code)

   - names, history (room codes where entry was stopped and so on)
   - evidence records and the log lines backing them (without waiting for the 90 days; for records newer than 30 days, the name, room code, log lines and detection text are removed at once and the rest is kept until 30 days)
   - copies held by the author and admins (not erased automatically yet. Copies on the author's PC: the author erases them by hand when asked. Copies on an admin's PC: the author asks the admin to erase them (the author can only ask). Names written in the ban console's audit log (`audit.log`) cannot be erased at present (a form that masks them is planned). Matching against the erase list automatically is also planned; 3.5, item 4)
4. What stays

   - For a **restriction that is still in effect**, the minimum needed to keep it working (hash, level, end date) and the evidence record for that restriction. If your erase request is still listed when the restriction ends, they go at once (a record younger than 30 days is emptied at once and deleted at 30 days). If it is not listed any more, they stay until the later of 30 days after the restriction ends and 90 days after the record was made (kept for good if it has no end).
   - **The count of violations** (PUID hash, count, dates). 1 year after the last restriction ends.
   - **Your line in the lift list** (the erase code and evidence number from when your appeal was accepted). Even if you ask for erasure, it is not removed for at least 180 days (removing it would leave the restriction in place on host PCs that did not start in the meantime). It stays in GitHub's history after that (Article 8(3)(4)).
   - **Lists the host writes by hand** (`Banlist.txt` and others). Please ask that host.
   - **Your name written in someone else's evidence record**
   - **Records on PCs running old PocketRoles (before v0.5.5), or on PCs that are no longer started**
   - **Your name in the game logs on a host's PC (and their log archive), in `events.log`, in report zips and "One player's evidence" zips on the desktop, and in the Client's recent people (`recent.json`; planned)**. The erase list does not erase these. They go away automatically after 30 days.
5. Please note

   - The code is not a password. Anyone who sees your code can ask for erasure in your place.
   - The author and admins also receive other people's codes, in the evidence records inside report zips (3.5).
   - Only what is in item 3 is erased. A restriction still in effect stays in effect.
6. We cannot see records on hosts' PCs. So even if asked, we cannot show records on a host's PC.
7. When asked, we respond without delay. If we cannot, we tell you why.
8. You can make the requests in this article without an account, and even if your account is limited.

## Article 13 (If information leaks)

1. If information leaks, is lost or damaged, or might be, we act quickly to stop it spreading and look for the cause.
2. When the law requires it, we report to the Personal Information Protection Commission and tell the people affected. For example:

   - when someone may have broken in on purpose
   - when more than 1,000 people are affected
   - after the 2026 amendment of the law starts, when information was given to others in a way the law does not allow (whether the way the central lists are published is such a case has not been settled yet; Article 8(4))
3. For people we have no way to contact, we announce it on the site.
4. We have a procedure for when information leaks, or may have leaked, from a staff member: pause or remove that member at once, hold the requests that member recently opened, revealed or replied to so they are not deleted (Article 5), investigate the scope using view and reveal records, and check whether affected people must be notified and a report made to the Personal Information Protection Commission.

## Article 14 (Questions and complaints)

- Contacts

   - The contact page on the site (support.starpocketgames.com). Choose the kind and send. No account is needed
   - Email `support@starpocketgames.com` (please start the subject with "Personal information")
   - A ticket on the Discord server "PocketRoles 役職部屋"
- Requests from hosts (shared-ban requests, report zips) go by email to `support@starpocketgames.com`. Appeals, erase requests and security reports stay with `support@starpocketgames.com` above.
- Japanese, Chinese or English are all fine.
- We are not a member of an "accredited personal information protection organisation".

## Article 15 (What we show at installation)

1. The first time StarPocket Client opens, before it installs or sends anything, it shows these screens in this order:

   1. "Terms of Use and Privacy Policy": a summary and the full texts, the versions and dates, a "Use chat translation" checkbox (**unticked at first**; nothing is sent unless you tick it; if you upgraded from v0.5.4 or earlier, see 3.9), an "Automatically report players removed for cheating to Innersloth" checkbox (**unticked at first**; nobody is reported automatically unless you tick it), an "I have read and agree to the Terms of Use and the Privacy Policy" checkbox (unticked at first), and "Agree"
   2. About Aegis: what it checks, what it does not do (no driver, no restart, PC scan results not sent), and "Install"
   3. Installation: progress, with a one-line explanation of what is happening now
2. Changes to PC settings (starting the Client when the PC starts, making a desktop shortcut and so on) are made only when you choose them.
3. There is a way to uninstall (settings, "General" → "Uninstall"). You can also uninstall from the first screen, without agreeing. If you do not agree, the official Client closes, and it sends nothing (Terms of Use Article 3(3)). The first time you open it, nothing is kept. If you do not agree after the terms change, what is already installed (the mod copy, settings, the earlier consent record, logs) stays as it is. To remove it, press "Uninstall".
4. If we add something new that sends information, the Client asks you again.
5. Code signing: the programs we distribute (such as StarPocket Client) are not code-signed today (code signing uses a certificate to show Windows who made a program; it is separate from the signature we put on the definitions file). We applied to SignPath Foundation for free code signing, but were not accepted this time. We have not decided whether to apply again.

## Article 16 (Cookies and similar)

1. Pages of the site that you only read use no cookies other than one that remembers the language you chose (1 year) (the official site stores your language and display choices in the browser; 3.6).
2. The contact page, the receipt-number page, the account pages (and the support chat (planned)) use the following. All are only what is needed to make the service work.

   - the language cookie (1 year)
   - Cloudflare Turnstile (bot check)
   - when signing in with Discord, a mark kept only between leaving for Discord and coming back, and a mark that shows the result of connecting Discord once on the screen (2 minutes)
   - the secret link token that lets you read replies (only in that browser)
   - a sign-in cookie while signed in (30 days) and a device mark (planned; Article 5)
3. On a PC shared with family, links stay in the browser history. Sign out when you finish.

## Article 17 (Changes to this policy)

1. This policy always shows a version number and the date it takes effect. Each time we change it, we add to "Change history" on the site the version, the date, a summary of the change and the differences from the previous version. Older versions can be read there too.
2. When we change this policy, we announce what changes and when, in advance, on the site and on Discord. Once StarPocket Client's news (planned) exists, we announce it there too. The copy of this document inside the Client is updated when you install a new version of the Client.
3. For important changes, such as sending more information or to more places, or using information for more purposes, we ask for your consent again in StarPocket Client, the StarPocket Account and the support chat.

   - If we change a purpose so much that it can no longer be said to be related to the previous purpose, we get the person's consent before using it that way (Act on the Protection of Personal Information, Articles 17 and 18).
   - When we send information abroad based on consent, we ask for consent for each new recipient (Article 28).
   - For people who have not agreed, we do not start the new sending or the new use.
4. Corrections that do not change the meaning, such as typos, are only added to the change history.
5. The correct text of this policy is only the text on the official site, in the official GitHub repository, and inside the official StarPocket Client (signed by us).

## Change history

| Version | Date | Changes |
|---|---|---|
| 0.1 (draft) | 2026-09-22 | First draft |
| 0.2 (draft) | 2026-09-22 | Added the StarPocket Account (3.2). Chat translation is now decided by a checkbox on the first screen (ticked at first). Added the consent record, age handling in support, the email-sending company and the change rules |
| 0.3 (draft) | 2026-09-22 | Applied the review. The summary's periods and age now match the body. Sending abroad is now a per-company table (basis, company and server countries, measures). What report-zip evidence records contain, what Aegis checks and when, what the erase list does not reach, and how friend-code hashes are treated now match the facts. Added Google Fonts, the Discord count, Ticket Tool and Cloudflare's records. Added the check for people under 16 and "best interests". Added "tell you what we use it for" |
| 0.4 (draft) | 2026-09-22 | Applied the owner's decisions. The site serves its own fonts, and Google Fonts was removed from the recipients (3.6, Article 6). Said why report-zip evidence records keep the erase code (3.3). Added that staff may use AI (Anthropic) for reply drafts and translations, what is sent, and that it happens only when the button is pressed (summary, 3.1, Article 5, Article 6, Article 10). Said the automatic reporting checkbox starts ticked (summary, 3.9, Article 15). Added stopping the match for a certain cheat (Article 9(6)). Added the conditions for making a shared ban from other hosts' records (3.5). Removed "no-services mode" (Article 15(3)). In the same version's review: said friend codes and similar details are hidden in text sent for staff drafts (3.1, item 11; Article 6); said "Repeat" detections also fall under the 2-host conditions (3.5); marked that the site still loads Google Fonts as a point to fix (3.6); said the match "may" be ended (there are limits; Article 9(6)); said that not agreeing after a change does not remove what is installed (Article 15(3)) |
| 0.5 (draft) | 2026-09-22 | Applied the owner's decisions (D-35, D-36). Said that PocketRoles compares your own Among Us account signed in with the shared-ban list inside that PC, sends nothing, uses the PUID and friend code only in memory, and that the launchers read the Steam account number to tell which line to use (summary, 3.4, 3.9, 3.11, Article 8(3)). Added `access-status.txt`, where the result is written (3.10). Said that asking our server is not done now, that before it starts we will change the policy and ask for consent again, and what would be sent (3.9). Added what happens to a host whose reports are found to be wrong on appeal 5 times within 30 days, and the record kept for counting (3.5, Article 5). A review the same day fixed these: the record for counting is now the one the ban console really uses (`reporters.txt`: the host's name and PC mark, deleted after 30 days ; there is no `overturns.json`); `access-status.txt` no longer says its marks "cannot be turned back", and says that its contents are encrypted, that someone using the same Windows user may read them with some effort, and that a line is deleted after 30 days without sign-in (3.10); PocketRoles in the game reads the Steam account number too (3.11); when the definitions file is fetched again (3.9); for the later server check, the form that sends nothing and only receives the list is recommended, and what the server could learn with the asking form is written (3.9) |
| 0.5 (draft) | 2026-09-23 | Applied the owner's answer (D-36): if we later also check with our server, PocketRoles will receive the whole list, and we will not use a form that asks "does this account have a shared ban?" (3.9) |
| 0.6 (draft) | 2026-09-23 | Applied the owner's decisions (D-37, D-38, D-36 item 3). Aegis evidence records on a host's PC are now kept 90 days for everyone (was 30 days; for a restriction still in effect, 90 days after they were made or 30 days after it ends, whichever is later). Logs still go after 30 days; only the 2 log lines that back a record now stay with it. An erase request still deletes at once (Summary, 3.3, 3.10, Articles 5, 9 and 12). Report-zip evidence now covers the last 90 days (the cap stays 200 records and 5 MB; older records go in a "One player's evidence" zip) (3.3). Added the "One player's evidence" zip for appeals (only that person's records, the log lines backing them and the PC and game versions; the person is confirmed by the erase code in the records; neither the friend code nor its hash; deleted after 30 days) (3.3, 3.5, 3.10, Article 5). Added that if a host gets a shared ban while their room is open, they leave at once even mid-match when the reason is cheating, and after the match for other reasons (Article 9(7); former items 7-9 are now 8-10). Erase-list lines now stay listed 90 days or more (Article 5; so they reach evidence records kept 90 days). Corrected the same day by a review: the report zip cap is what the launcher really does, 200 records and 5 MB, not 600 and 15 MB (3.3); a host can make a one-player evidence zip at any time, an appeal sends every record of you on that PC, and the erase code does not prove who sent the appeal (3.3); when an erase request is still listed as a restriction ends, the records go at once (Article 12(4)); Article 9(7) now uses the reason words a shared-ban line really carries |
| 0.7 (draft) | 2026-09-23 | Applied the owner's decisions (D-39, D-40, D-41, D-42). Said that the check on running apps now looks not only at names but also at the program file on disk - its content fingerprint (SHA-256), embedded version info and signer name (3.11; from v0.5.5; whether something is Microsoft's is decided by whether its certificate chains to a Microsoft root, not by the displayed signer name; only apps in your own Windows session are checked; nothing is sent anywhere). Chat translation and automatic reporting are now both off by default (summary, 3.9, 3.10, Article 15(1)(1); this overrides the decision of 2026-09-22). Changed the shared-ban conditions (3.5: one host for "Certain" with at most 2 per 30 days from the same host; 3 or more different hosts within 30 days for anything else; reports wrong on appeal 3 times within 30 days). The brand is spelled "StarPocket". Once a support platform is chosen, a section on receiving support (what reaches the payment company, refunds, how long records are kept) will be needed. Corrected the same day by a review: the summary line for chat translation now names its default (off), and both summary lines and the 3.9 table now say today's build still has it on, so it sends unless you turn it off (Summary, 3.9). The program-file explanation was split from one long line into five (3.11). The shared-ban-gate description now matches the branch (3 within 30 days, comes back by itself with time, `reporters.txt` pruned at 30 days, "Certain" capped at 2 per 30 days; what is left is `MinHosts` 3 and merging the branch into v0.5.5) (3.5; that "`MinHosts` 3" was withdrawn in 0.8 - the number stays 2) |
| 0.8 (draft) | 2026-09-23 | Put the number of different hosts a shared ban needs back to **2 or more** (3.5). The "3" had been written into the drafts without ever asking the owner (the official site was not published yet at that time, so it never went out on a published page); on 2026-09-23 the owner answered "keep 2 for now - with so few hosts, 3 would mean shared bans almost never happen; raise it later when there are more hosts" (D-39). Said the number may go up as more hosts join, and split the three numbers that sit together into **hosts** (2 or more different hosts), **bans** (2 per 30 days from the same host) and **mistakes** (3 times within 30 days). The bans and mistakes numbers are unchanged. Also corrected the note about the ban console: the host count (`GateRules.MinHosts = 2`) already matches, and all that is left is merging shared-ban-gate into v0.5.5 (3.5). In the same day's review, one row was added to Article 5: other people's names, account names and URLs written in a report of a fake download site are deleted 30 days after the report has been checked and dealt with. No new intake is built; one label is added to the existing support requests instead (`SUPPORT-SPEC` §5.8) |
| 0.9 (draft) | 2026-09-24 | Followed the renumbering of the Terms (Article 10 became Article 13). Added item 4 to Article 7(2) (handing over the running of the Services; Terms of Use Article 27, Act on the Protection of Personal Information Article 27(5)(ii)) |
| 1.0 | 2026-09-26 | The first published version. **The text itself is unchanged from 0.9.** The "(draft)" marks, the lines explaining those marks and the "not published yet" wording are gone, and the date it takes effect is filled in. |
| 1.0 | 2026-10-02 | Removed the fragments and working notes the clean-up had left behind (the sentence in 3.5 that broke off at "in item 4" and a check mark there, a note in 3.9 confirming a default, and a working note in the Discord row of the table in Article 6). Corrected the chat-translation explanation (summary, 3.9, Article 15) to the facts: the setting of a host who upgraded from v0.5.4 or earlier and never changed it may stay on, and reinstalling or updating the mod with StarPocket Client (v1.1.2 or later) applies the answer from the first screen. **The policy itself is unchanged.** The version stays 1.0 |
| 1.1 | 2026-10-04 | Announced on 2026-10-04 and in effect from the same day, 2026-10-04. Terms of Use Article 26(3) says that changes worse for users are announced at least 14 days before they take effect. We put this version 1.1 into effect from the day it was announced after making sure that no outside user had agreed to 1.0 and was using our services (we looked at the StarPocket Client downloads, the accounts and support requests on the support site, the shared-ban list, the erase list and the lift list, appeals and reports sent by email or on Discord, and the members of the Discord server; all of them were the author's own tests). From the next change on, we will announce changes at least 14 days in advance. The copy of this policy inside StarPocket Client will become 1.1 with the next version of the Client. Until then, the text inside the Client stays at 1.0, but from 2026-10-04 the version that applies is this version 1.1, which is in effect (Terms of Use Article 31(4)). Main changes from 1.0: (1) the old hand-written page on the site is retired and this document has the same text on the site, in StarPocket Client and on GitHub; (2) "(planned)" removed from what runs (the contact page, accounts, the home server, Turnstile, the lift list) and the text matched to what really happens (6-digit verification code, 10 minutes; the IP mark kept 1 day; requests kept 30 days after resolution; people who connect Discord keep both email and Discord; staff AI drafts off today; where the sites are hosted; contacts); (3) what comes next written as "(planned)" (the support chat with AI bot and article search, live replies, 7-day auto-resolve and the delete button, the delete-account button and automatic deletion of accounts unused for 2 years, new-device notices and the device list, email address change, support members, two-step verification); (4) added applying to the Steam-copy program (planned; the day requests open will be announced in a notice), with translation by DeepL SE for applicants only; (5) the Resend row now lists the kinds of email and the embedded logo image, and says that the notice when Discord is connected contains the Discord display name; (6) on the lift list, 1.0's "so that others cannot tell you were restricted before" is withdrawn, and it now says that hosts of rooms you joined can find out, that it stays in GitHub's history, and that you can ask not to be listed (Article 8(3)(4), Article 12(4), 3.10); (7) in the Chinese version, the list terms now match the mod's settings screen ("名单"; no change of meaning); (8) what is not built yet (deleting imported records, report zips and email after 90 days, matching against the erase list, checking your own shared ban and the host's PocketRoles leaving the room) is written as "planned", "we will check this before publishing" in Article 8(4) and Article 13 now says the lists are already published and the question is not settled yet, and "to be checked" in the Article 6 table became "being checked"; (9) server backups now have their real periods (nightly backups on the home server: the newest 14; backups carried to the author's PC: the newest 30 (gaps in creation or retrieval can leave backups older than 14 or 30 days); backups before switching versions: 14 days (longer while there is no switch); backups the author takes by hand: until deleted by hand; longer than 1.0's "3 days"), and it now says that deleted requests and accounts, and the marks made from IP addresses and email addresses, stay in backups for these periods (Summary, 3.1 item 9, Articles 5 and 12); (10) people who write a reply-to email address on the contact page get an email saying the request was received (planned); the subject and message they wrote are not included, at most 3 a day go to the same address, a mark for counting is kept for 1 day, and "to tell you we received it" was added to what the email address is used for (3.1, Articles 5 and 6); (11) reply emails show the handler's name (a staff display name), the team name, the handler's icon and a stamp, and people who sent while signed in get a way into the "Your requests" list (planned; the reply text is still not included, as in 1.0) (3.1 item 7, Article 6); (12) the author adds support members by email address or Discord ID, support requests are split into 9 teams, and a support member reads only the requests of their own teams (no reply email address, account ID, or room, user or room-access admin screens; room-access appeals only for members with the Room Access Team tag); the four staff safeguards (two-step verification, the staff pledge, replies held for the author's check, and hiding requests that arrived before a member was added) are built together with staff and teams (Articles 2 and 11; planned); (13) the notice that a reply has arrived is also sent to the account's email address (3.1 item 7, 3.2; planned; today only the reply-to address); (14) backups the author takes by hand will be deleted automatically after 90 days, USB copies older than 90 days are deleted the next time a copy is made (longer while the USB stick is not connected), and backups will be encrypted with a key file (the locking key on the home server; the unlocking key and its passphrase only on the author's PC) (Articles 5, 11 and 12; planned; today, until deleted by hand); (15) sign-in from StarPocket Client and StarPocket Friends (what is sent, what is kept, who sees it, the rules for each age band and the parental check) written entirely as "(planned)" (Summary, 3.2, 3.6, 3.9, 3.10, new 3.12, Articles 4, 5, 6, 7 and 10); (16) support members who read requests must be 16 or older and live in Japan (ages 16 and 17 need a parent or guardian's consent; unpaid), and leak safeguards were added (contact details in messages masked at first and reveals recorded, name and time watermarks, viewing records kept for 1 year, the author alerted and access stopped when many requests are opened in a short time, no export interface for support members, and a leak procedure) (Articles 2, 11 and 13; planned); (17) added translating support requests (the paid DeepL API Pro): only requests whose senders ticked "may be sent to DeepL for translation" when sending them (this tick is planned); text is sent only when the author, or a staff member who can read that request (today, the author alone), presses the button, with contact-like and number-like parts hidden; translations are not kept, and the only record is who translated how many characters of which request; up to 20,000 characters per person per day (Summary, 3.1 item 12, Articles 5 and 6); (18) added a section on "Join us" (contributor recruitment) applications (what is collected, that only the author reads them, notices to the author, selection and interviews, parental consent for ages 16 and 17, the pledge after approval (16 items; version 2026-10-04), the author translating applications (planned), how long they are kept (applications with no decision are withdrawn automatically 90 days after the last activity; deleted 90 days after not being selected or withdrawn; approved people's records until 1 year after they stop), applying again and withdrawing) (Summary, Articles 1 and 2, 3.8, 3.13, Articles 4, 5, 6, 10, 11 and 12); (19) on code signing, it now says that our programs are not code-signed today and that we applied to SignPath Foundation but were not accepted this time (Article 15(5)). The line-by-line differences are on the site's "Change history" page |
| 1.2 | 2026-10-07 | Announced on 2026-10-07 and effective from 2026-10-08 00:00 Japan time, midnight on the next day. The announcement is on the preceding day, but leaves less than 14 days. Its relationship to Terms of Use Article 26(3) on changes worse for users and to the advance-notice wording in this table's 1.1 row will be checked with a legal professional. The reasons for this effective date are: (1) According to the owner's confirmation, the only outside user of the parts changed here (support-site accounts, requests, room restriction appeals, "Join us" applications and the Discord server) was one translation contributor, who has already been told what 1.2 contains. This is not a count of everyone who merely visited the sites or downloaded from GitHub. (2) Newly added staff do not see requests received before they were added. Staff see only appeals saying the restriction was a mistake, meeting the time conditions in Article 2(4), and submitted through a form with the new viewing notice version 1.2. Each member's addition time is separate from the shared start times and does not change when a pause is lifted. The appeal access start time must be explicitly set to the effective time or later; if unset, empty or invalid, appeals are author-only. Old, missing or unknown versions remain author-only even if received after introduction. Erasure requests and reports about a host remain author-only whenever received. All existing admin and staff permissions have not been investigated, so this does not assert that every reader of earlier requests stays unchanged. (3) Discord DMs go only to people who choose them. The choice explains sending to Discord Inc. (United States), links to this policy, and asks people under 16 to decide with a parent or guardian; the choice is treated as consent to sending (Article 17(3); Terms of Use Article 26(4)). DMs, staff access to appeals and the add-staff list are planned to be put on the server after the 1.2 announcement and effective time and provided only after the effective time and production checks. The pre-launch test of sending and receiving a DM with the real bot has not been done. The current StarPocket Client (1.1.2) still contains version 1.0 of the documents. The next Client release is planned to include version 1.2 (date not yet set). Main changes: (1) updated the description of admins and support members reading their teams' requests, the four staff safeguards and leak prevention; (2) limited the add-staff list to "Join us" applicants, stating that display names, sign-in methods and last sign-in dates are used for selection and registration; (3) stated the Room Access Team's assessments and reply drafts, staff addition times, shared start times, saving and checking the viewing notice version, author-only erasure requests and host reports, and that in-game names, room codes and host names are not masked; (4) stated optional Discord DMs, including application replies, overseas sending and handling for under-16s, 30-day choice/withdrawal action records and 90-day sending-attempt and result records including attempt numbers and unknown deliveries, without Discord IDs or DM text in the record; attempts at least 15 minutes apart and at most 3 including the first only for temporary errors known to be unsent; no resending of the same notice for unknown delivery; counting attempts in progress and unknown deliveries toward interval and attempt limits; resuming from the next reply without resending past notices, and stopping individual choices with the stop-all button; the 7-day pause after 3 consecutive failures, and clearing the choice when Discord is unlinked or a different Discord is linked; (5) updated "Your requests", the contributor pledge page and version records; (6) stated retention for pending replies, leak-investigation holds, staff pledges and authenticator records. Line-by-line differences can be read on the site's "Change history" page |
| 1.3 | 2026-10-16 | Takes effect on 2026-11-01 00:00 (Japan time). Because some changes disadvantage users, we announce them at least 14 days before effectiveness under Terms Article 26(3). Support chat and the bot remain planned. We align sign-in, server-side search and 24-hour retention from the start, backups, the initially ticked attachment choice, author-only access, appeal-form routing, agreement versions and times, the adult-only bot in allowed countries, limits and sent data, replies and notices, cost/usage/operation records, DeepL handling and guidance for children with the implementation. The under-16 staff-draft rule and appeal-form access and 7-day friend-code rule remain unchanged. Two Chinese terms are aligned. Important changes, such as information sent or kept, apply only to people who have received an explanation and agreed, in accordance with Terms Article 26(4). |
| 1.3 | 2026-10-29 | Changed the contact email address (messages sent to the previous address will still reach us). |
