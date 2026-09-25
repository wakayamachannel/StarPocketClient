# StarPocket Games Privacy Policy

- Version: 1.0
- Last edited: 2026-09-26
- Takes effect: 2026-09-26

> "(planned)" = a feature not built yet. By the day that feature is released, this document will be made to match what is true.
> The Japanese version is the original.

---

## Summary

- We install nothing and check nothing on the PC or phone of people who only join a room.
- Records on a host's PC stay only on that host's PC. They do not reach us.
- Aegis evidence records on a host's PC (the record made when someone is removed or restricted) are kept 90 days for everyone, because appeals can arrive late (the records of a restriction still in effect may stay longer; 3.10). Other records, such as logs, are deleted after 30 days.
- PocketRoles checks, inside that PC, whether your own Among Us account that is signed in is on the shared-ban list. Nothing is sent.
- What reaches us is only what you send yourself (support messages, report zips, appeals) and the information of an account you made yourself.
- Support chat conversations are deleted 30 days after the last message (appeals in progress and emails: up to 90 days). Our servers do not store IP addresses.
- The support bot is an AI. What you write is sent to a US company called Anthropic. When staff have AI write a draft reply, it is also sent to that company, only when a staff member presses the button. Do not write your real name, address, phone number, school or passwords.
- Chat translation is **off by default**. When it is turned on, the room's chat text is sent to Google or DeepL. You can turn it on from the first screen of StarPocket Client and in settings. That is for a fresh install: the config file of someone upgrading from v0.5.4 or earlier stays on (3.9).
- Players removed for a "Certain" cheat in a room are reported to Innersloth (**off by default**). You can turn it on from the first screen of StarPocket Client and in settings. Every way of installing starts off (3.9).
- You do not have to make a StarPocket Account. If you do, it is not linked to you in the game.
- To have your records erased, type `/cmd id` in a room's chat and send us the code it shows.
- If you are under 18, read this together with a parent or guardian.

---

## Article 1 (About this policy)

1. This policy explains how StarPocket Games ("we", "us") handles information about individuals in:
   1. Software: PocketRoles (the mod), Aegis, today's launcher, StarPocket Client
   2. Websites: the PocketRoles site, the StarPocket Games site (planned)
   3. Support: email, Discord tickets, the support chat and AI bot (planned)
   4. The StarPocket Account (planned)
   5. Central lists: the shared-ban list, the erase list and the lift list (planned) in the definitions file
   6. The Discord server "PocketRoles 役職部屋"
   7. The home server (planned): the support chat, accounts, the room list and so on
2. This policy keeps two things apart.
   1. **Information we handle**: what reaches us (Article 3, sections 3.1 to 3.8)
   2. **Information a host handles on their own PC**: what the Software records on the host's PC, and what it sends directly from the host's PC (Article 3, sections 3.9 to 3.11)
      - This does not reach us.
      - But because we decide the Software's default settings and explanations, we describe it in detail here.

## Article 2 (Operator)

1. Name: StarPocket Games
   - The activity name of one individual developer. Not a company.
 - The author's activity name: もみじちゃ (Momijicha)
2. Name and address: given without delay on request. Contact us through Article 14.
3. Person responsible for personal information: the author
4. Staff: the author and the admins the author has asked to help (the Aegis team)
   - Admins help with support and appeals, following the author's instructions.
 - For now, only the author can read the text of support chat messages. If we extend this to admins, we will change this article and tell you.
   - Admins promise to keep things confidential. There are three parts. They do not tell anyone what they read for this work. They keep the promise after they stop being an admin. If they break the promise, they are removed as an admin.

## Article 3 (Information we handle, and why)

### 3.1 Support (email, Discord tickets, the support chat)

| Information | How we get it | Why |
|---|---|---|
| What you write, the kind (bug, question, appeal and so on), language | You write and send it | To reply. To review decisions and appeals. To erase records |
| Code (R-31 and so on), app version, language | Already filled in when opened from "Contact support" in the Client or on the site. You can remove it | To find the problem faster |
| Names of what was found (tool or DLL file names) | Only when you write them in the "Names shown on the screen" field (optional; the Client never fills them in) | To fix false detections |
| Friend code (appeals only) | Written in its own field | To match against Aegis records |
| Attachments (report zips, images) | You attach them | To look into bugs and decisions |
| Email address (only if you write it) | You emailed us, or wrote it in the chat's field | To tell you a reply has arrived |
| Discord name and ID | You opened a ticket | To reply |
| Your age answer (under 16 or not) | Asked when starting a chat | To decide whether the bot and AI drafts are used and whether a parent or guardian needs to confirm |
| Versions of the terms and policy you agreed to, and the time | Kept automatically when you press "Agree and start" or "Agree and send" | So we can show later that you agreed |
| Ticket number (SP-0042 and so on), secret link token, times | Made automatically | So you can read replies |

**Support chat and AI bot (planned)**

1. An AI bot answers first.
   - The bot uses Claude by Anthropic PBC (United States).
   - The bot is not a person.
2. The bot answers only from our FAQ and manuals.
3. What you write and the bot's answers are sent to Anthropic to make the answers.
   - Anthropic promises not to use content received through its API to train its AI.
   - Anthropic states that it deletes received content automatically within 30 days.
   - However, content judged to break Anthropic's usage rules may be kept for up to 2 years. Safety scores (trust and safety classification scores) may be kept for up to 7 years.
4. Before a chat starts, we show this on the screen and ask you to press "Agree and start" (Article 6).
5. AI is used for staff reply drafts (item 11) only for people who ticked "You may use AI for the reply draft" when they sent their request. On top of that, for the following people the bot is not used; staff answer from the start. Even if they ticked it, AI is not used for the draft, so these people's messages are not sent to Anthropic.
   - People who answered "under 16"
   - People using it from mainland China (recognised only by the country code Cloudflare adds; the country code is not stored)
6. When you press "Ask a person", staff reply on the same screen.
   - The staff Discord channel is told only "ticket number, kind, language". The message itself is not sent there.
   - The author reads the message and writes replies on a staff page (if this is extended to admins, see Article 2).
7. Replies can be read on the same chat screen (the secret link). People with an account can also read them on their account page.
   - Only if you wrote an email address, we also tell you by email that a reply has arrived. The email does not contain the message.
8. Staff do not read conversations that ended with the bot only. The only exception is when we look into attacks or abuse: then only the author reads them, and the fact that they were read is kept in the action record of the staff page.
9. To prevent abuse, we use:
   - a bot check (Cloudflare Turnstile)
   - sending limits
   - to count sends, the IP address is turned into a form mixed with a "password for that day only" and used only in memory. It is never written to disk and disappears after 1 day.
10. The server never opens URLs written in messages.
11. **Staff reply drafts and translations (AI)** (planned)
    - Staff may have AI (Claude by Anthropic PBC, United States, used through its API) write drafts or translations of replies.
    - Something is sent to the AI only when a staff member presses the "Draft with AI" button. Nothing is sent automatically.
    - What is sent: the text of that support request (the conversation so far), its kind, language and code (R-31 and so on)
      - Friend codes, email addresses, URLs and long numbers written in the text are hidden before it is sent (the same as 3.5, item 5).
    - What is not sent: email addresses, Discord names and IDs, friend codes (whether from the special field or written in the text), attachments (report zips, images), ticket numbers
    - A staff member always reads and edits the draft, and a staff member sends it. The AI never sends a reply by itself.
    - The AI is never allowed to decide room restrictions, shared bans, appeals, limits on the Services or erasing records. Staff always decide those.
    - How Anthropic handles it (not used for training, days until deletion) is the same as for the bot in item 3.
    - In the support chat, this is also shown on the screen before the chat starts, and we ask for your agreement (Article 6). Support requests that came by email or Discord ticket are not sent to the AI until the basis for sending abroad (Article 6(3)) is settled.

### 3.2 StarPocket Account (planned)

You do not have to make an account (Terms of Use Article 8).

| Information | How we get it | Why |
|---|---|---|
| Email address | Written by people who sign in by email | To send sign-in links. To send replies and notices |
| Display name | You choose it (does not have to be your real name) | To show on screen |
| Date created, date of last sign-in | Automatic | To delete accounts not used for 2 years |
| Numbers of linked support requests | When you contact support | To show your requests in one place |
| Discord user ID and name | Only people who sign in with Discord or connect Discord | Sign-in. Reply notices |
| Passkey public key and ID | Only people who register a passkey | Sign-in (fingerprint and face information never leaves your device) |
| Sign-in records (time, browser kind such as "Chrome on Windows", whether the device is new) | Automatic (IP addresses are not stored) | To tell you about sign-ins from unknown devices |
| Device mark (a number stored in the browser) | Automatic | To tell whether a device is new |
| The "together with my parent or guardian" confirmation | Ticked by people under 18 | To confirm the account was made together with a parent or guardian |
| Your answer to "Are you under 16?" | Asked when making an account | To decide whether a parent or guardian needs to confirm (Article 10) |
| Versions of the terms and policy you agreed to, and the time | Kept automatically when you press "Agree and create" | So we can show later that you agreed |

- **Your account is not linked to you in the game.** Your Among Us account, friend code, PUID, in-game name and Aegis records are not put in the account.
- We do not use accounts to show ads or give information to other companies.

### 3.3 Report zips and "One player's evidence" zips

1. Pressing "Make a report zip" in the launcher or the Client makes a zip on the host PC's desktop. **It is never sent automatically.** You decide whether to send it.
2. Contents
   - Game logs, launcher records, PC and game versions
 - Aegis evidence records from the last 90 days (the newest 200, up to 5 MB; older records go in a "One player's evidence" zip, item 5), and, for records older than 30 days, the two game-log lines that back each one (in place of the deleted log) (3.10)
3. Left out or masked
   - Left out: API keys, Discord webhooks, settings files, passwords
   - Masked: PUIDs, friend codes, the start of hashes, erase codes (in logs), places containing the Windows user name
   - Evidence records contain the player's name, the hash of their PUID and their erase code (not the friend code or its hash).
   - The code is left unmasked so the author can handle erase requests: by matching it against the codes on the erase list, the author also erases that person's records kept by the author and admins (3.5).
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
| Shared-ban list `[bans]` | PUID hash, level, end date, short reason word | To restrict the same person in every PocketRoles room. Also, PocketRoles checks whether your own account signed in on that PC is on it (compared only inside that PC; 3.11) |
| Erase list `[erase]` | Erase codes and dates | To erase records on each host's PC |
| Lift list (planned) | Evidence number (AEG-…) and date | To lift, on each host's PC, automatic restrictions found to be mistakes |

- Names and friend codes are not on them.

### 3.5 Ban console (the author's and admins' PCs)

1. Evidence records from report zips and "One player's evidence" zips (3.3) sent to us are imported into the ban console on the author's and admins' PCs.
   - Imported evidence records contain the player's name, the hash of their PUID and their erase code (3.3). So the author and admins also receive the codes of other people, not only of the person who sent the zip.
   - The codes are used to match against the erase list, so the records of people who asked for erasure are erased (today's ban console does not match them yet;  in item 4).
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
4.  Today, imported records and the action record have no expiry, and the erase list is not applied to them. Before publishing, we will:
 - delete them 90 days after the case ends
   - erase records for codes on the erase list
   - make names in the action record maskable

5. Reply drafts (AI): only when the author, unlocked as the author in the ban console, presses "Draft with AI", the reply's kind, language, ban number, rule category, dates and template are sent to Anthropic PBC (United States) to make a draft.
   - The addressee's and the staff member's names are replaced by placeholders (NAME_1, STAFF_1), and so is a shared ban line's number (the first 8 hex digits of a hash; BANID_1); they are put back into the returned draft on the author's PC.
   - PUIDs, friend codes, hashes and evidence files are not sent.
   - The box for pasting the support request's text is not shown until the basis for sending it abroad (Article 6(3)) is settled. Once it is used, friend codes (their name part too), e-mail addresses, URLs, @names and long numbers are hidden and the addressee's, the recorded and the staff member's names become placeholders before it is sent; anything else in the text that cannot be hidden, such as other people's names, is sent as written. Before it first goes out, the author sees exactly what is sent. Text from people under 16 and from people in mainland China is not pasted (3.1, item 5). Text of requests that came by e-mail or Discord ticket is not pasted until the basis is settled.
   - The draft is checked on the author's PC (a draft that does not keep the template's result paragraphs word for word, has words against the result, has a number, date, e-mail address, URL or the like that the template does not have, or leaves out the template's number or dates is not used), and a person sends the reply. The ban console never sends a reply.
   - Anthropic's handling (no training, deletion period) is the same as in 3.1, item 3. [Check: confirm against Anthropic's commercial terms before publishing (OWNER-DECISIONS, "Anthropic に聞くこと" 4)]

### 3.6 Websites

1. The site is on GitHub Pages (GitHub, Inc., United States).
   - GitHub states that it records visitors' IP addresses for security.
   - Text uses the fonts already on your device (computer or phone). Nothing is loaded from other sites or companies, fonts included (no Google Fonts).
2. The site stores only these 2 things in your browser:
   - the language you chose
   - light or dark display
3. We use no ads, no analytics and no social media buttons.
4. On the support chat and account pages (planned), Cloudflare Turnstile runs as a bot check (3.1, item 9).

### 3.7 Discord server

1. The Discord server runs on the service of Discord Inc. (United States). Discord's privacy policy also applies.
2. What we see:
   - names, messages and roles in the server
   - what is written in tickets
3. Ticket transcripts are set not to be kept. Appeal tickets are deleted when decided.
4. StarPocket Client shows only Discord's "online now" count. It does not collect members' names. The request that gets the count is in 3.9.

### 3.8 Home server (planned)

| Information | Why | How long |
|---|---|---|
| Rooms listed in the room list (room code, server region, player count and capacity, room state (open, full, in game), room kind (with roles or normal rules), map, language, chat type (free or quick), mod and game versions, tags (chosen from a list)). No names, friend codes or free text are sent | To show the room list | Deleted when the room closes or is made private. Memory only |
| Host key (the public half) | To prevent impersonation in the room list | Deleted after 30 days unused. `/rooms forget` deletes it at once |
| Host events (key number, kind, region, time; no room code) | To look into abuse | 7 days |
| Records of room-code conflicts and of rooms hidden by staff (with the room code) | To look into abuse | 7 days |
| Support (3.1), accounts (3.2) | As in 3.1 and 3.2 | As in Article 5 |
| Server operation logs | To look into faults | 14 days. No IP addresses or message text |

- A room is listed only when the host chose "Agree" in the settings.

### 3.9 What the Software sends from the host's PC

These are sent directly from the host's PC to the other side. They do not reach us.

| What is sent | To | When | How to stop it |
|---|---|---|---|
| Requests to receive the definitions file (only receiving; nothing about this PC is sent) | GitHub (United States) | At game start, and once a day while running. When the tray Aegis starts. Also, when a room could not be created (while an update is required or a shared ban lasts) and when the online menu is opened during that time, at most once every 5 minutes (planned) | Cannot be stopped (as with any connection, GitHub receives the IP address). `RemoteRules=false` only stops the received detection numbers from being used; the file is still received for the erase list and for checking this PC's own account (3.11). The tray Aegis receives it whatever the setting |
| Chat text (up to 300 characters) and the target language | Google (United States) or DeepL (Germany) | When chat translation is used. Names and room codes are not sent | Untick "Use chat translation" on the Client's first screen. "Chat translation" in settings. `/opt translate off` |
| Room code, player count, state | The Discord webhook the host set | Only when the host set it | Do not set it |
| Official report (reason and the player) | Innersloth (United States) | When a player is removed for a "Certain" cheat (at most once every 30 days per player). On `/aegis report` | Turn it off in settings under "PocketRoles → Aegis", or with `[AntiCheat] AutoReport=false`. **The default in v0.5.5 is off.** The key `[AntiCheat] AutoReport` existed in no build up to v0.5.4, so an upgrader's config file does not hold it either: every way of installing starts off |
| Requests to get Discord's "online now" count (no names come back) | Our server (we do not connect to Discord directly) | When the Client's community panel is opened (planned). Our server gets the count from Discord once every 5 minutes and passes it on | Do not open the panel |
| A check to choose a region | Among Us servers | Only when `AutoRegion` is on | Off by default |
| Update checks, downloads | GitHub, BepInEx's download site | When you press "Check for updates", Install, Repair or "Update". **The Client does not check by itself at start** (Terms of Use Article 12(2)(2)) | Do not press |
| Requests to receive news and status notices (status.json and so on) | GitHub Pages | At Client start | — (only receiving; nothing is sent) |

- As with any connection, the other side's server receives the host's IP address.
- **Checking your own shared ban** (planned): PocketRoles in the game uses the definitions file it received to check, inside this PC, whether the Among Us account signed in is on the shared-ban list (3.11). Nothing new is sent for this.
  - Also checking with our server (by receiving the whole list) is **not done now**. If we start it, we will first change this policy and ask for consent again in StarPocket Client (Article 17(3)). The PC of anyone who has not agreed does not make these requests.
  - In the planned form, nothing about your account would be sent: PocketRoles would receive the whole list from our server and compare it inside this PC. As with any connection, the IP address would reach our server and Cloudflare.
  - We will not use a form that asks "does this account have a shared ban?" (sending the first 5 characters of the hash of the PUID and a number that changes every time). Because the list's hashes are public, the server could tell who is asking when someone on the list asks.
- **How chat translation is decided**
  - In StarPocket Client, the first "Terms of Use and Privacy Policy" screen has a "Use chat translation" checkbox. It starts unticked. Unless you tick it, nothing is sent.
  - You can change it later in settings, under "PocketRoles → Chat translation".
  - A fresh install defaults to "off" from v0.5.5 on (`Bind("Translate", "Enabled", false)`). But **the config file of someone upgrading from v0.5.4 or earlier stays "on".** BepInEx does not overwrite a value that is already in the file, and the file cannot tell "on because the host chose it" from "on because that was the default". v0.5.5 tells that host once and hands them `/opt upgrade off`, but changes no value.
- **How automatic reporting is decided**
  - On the first screen of StarPocket Client, under chat translation, there is an "Automatically report players removed for cheating to Innersloth" checkbox. It starts unticked. Only if you tick it does anyone get reported automatically (players are still removed from the room either way, as before).
  - You can change it later in settings, under "PocketRoles → Aegis".
  - (Checked) However PocketRoles is installed, the default in v0.5.5 is "off" (`Bind("AntiCheat", "AutoReport", false)`; the key `[AntiCheat] AutoReport` existed in no build up to v0.5.4, so an upgrader's config file does not hold it either). D-10, D-40.
- People who join have not agreed to anything. When translation is on, the welcome line says "Auto-translation is on" (not if the host changed or turned off the welcome line).

### 3.10 What the Software keeps on the host's PC

| Record | Contents | When it goes away |
|---|---|---|
| Restriction list `aegis-bans.json` | Name, hash, level, end date, rule, evidence number, erase code | Reduced 30 days after it is no longer needed. Kept while a restriction lasts (kept for good if it has no end). Only the count lasts 1 year after the last restriction ends |
| Evidence records `evidence\AEG-….json` | Name, hashes (of the PUID and of the friend code), erase code, time, room, server region, rule, numbers, 20 log lines about that player (for a callout notice, the first 40 characters of the player's own line). No IP addresses or other people's chat | 90 days (for everyone, restricted or not, because appeals can arrive late). Records a restriction still in effect points to: 90 days after they were made or 30 days after the restriction ends, whichever is later (kept for good if it has no end). With an erase request (Article 12), deleted without waiting for the 90 days (for ones newer than 30 days, the name, room code, log lines and detection text go at once and the rest after 30 days) |
| Lines backing an evidence record `evidence\AEG-….log` | Only 2 lines of the game log from when the record was made (the Aegis detection line and the line where the mod wrote the record; they hold a name, what was detected and the first 8 characters of the PUID hash). The author's ban console uses them to check that the record matches the log. No other lines are copied | Copied before the game log is deleted at 30 days; deleted together with the evidence record (at once with an erase request) |
| Host's lists `Banlist.txt` and others | PUID (or friend code if none) and name | Never deleted automatically (lists the host made) |
| Game logs | Names, room codes, join and leave times, Aegis lines, first 8 characters of PUID hashes, the game server's address | 30 days (only the 2 lines backing an evidence record stay with it, as `evidence\AEG-….log` above) |
| Report zips and "One player's evidence" zips on the desktop (3.3) | As in 3.3 | 30 days (the launcher deletes them) |
| Launcher record `launcher.log` | Launcher actions | When it is over 1 MB, deleted the next time the launcher opens (no limit in days). The same in the Client |
| Recent people and rooms `recent.json` (Client; planned) | Names, times, room codes | 30 days |
| Consent record `consent.json` (Client; planned) | Versions of the terms and policy agreed to, time, the chat translation and automatic reporting checkboxes | When the Client is uninstalled. Never sent anywhere |
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
   - PocketRoles in the game, the tray Aegis, today's launcher and StarPocket Client (planned) read the number of the Steam account signed in now from Windows settings (the registry), turn it into the short mark in 3.10 and the key that opens the line, and use it only to tell which line of `access-status.txt` to use. This is so that, on a PC shared with family, the screens don't show another person's ban or code. The number itself is not written down or sent.
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

## Article 5 (How long we keep things)

| Information | How long |
|---|---|
| Support chat conversations (including with the bot) (planned) | 30 days after the last message. Items "on hold" (such as appeals in progress) up to 90 days. You can delete them at any time with "Delete" |
| (Planned) friend code for an appeal in the support chat | Until matching is done. 7 days at most |
| Friend codes sent by email or in a Discord ticket | Deleted with that email or ticket (email: 90 days after the last message; tickets: when decided)  |
| Email address in a support request (if written) | Deleted with that request |
| Other people's names, account names and URLs written in a report of a fake download site | 30 days after we have checked it and finished dealing with it. Deleted together with that request |
| Versions and time of your agreement, your answer to "Are you under 16?" | Deleted with that support request or account |
| Account (planned) | Until you delete it. If you do not sign in for 2 years, deleted after a notice 30 days before |
| Account sign-in records | 90 days |
| Device marks | 1 year after last use, or when the account is deleted |
| Sign-in links | 15 minutes (stop working once used) |
| Email support (Gmail) | 90 days after the last message  |
| Discord appeal tickets | Deleted when decided |
| Notices in the staff Discord | 30 days |
| Report zips, "One player's evidence" zips (3.3) and imported records held by the author and admins | 90 days after the case ends  |
| Report-host record (the ban console's `reporters.txt`) | Lines of reports that were mistakes: 30 days. Lines of trusted admins: until the trust is taken back |
| Lines in the shared-ban list | Until the end date. Lines past their end date are removed within 30 days |
| Lines in the erase list | 90 days or more (so that they also reach evidence records, kept 90 days, on host PCs that have not started for a while; the newest 1000 are used) |
| Content sent to Anthropic (conversations with the bot, text staff sent for drafts) | Under Anthropic's rules, within 30 days (up to 2 years for content judged to break its rules; safety scores up to 7 years) |
| Home server operation logs | 14 days |
| Records of blocked attacks (Cloudflare; they contain IP addresses) | The period set by Cloudflare's rules (kept in Cloudflare's dashboard, where the owner can see them; not kept on our servers) |
| Server backups | 3 days |

- How long things stay **on a host's PC** is in 3.10: Aegis evidence records (and the 2 log lines backing each) 90 days; logs, report zips, copies of sent reports and the like 30 days. These are records on the host's PC, but we set these defaults.
- **About GitHub's change history**: the central lists are managed on GitHub. Even after a line is removed from a list, the old line stays in GitHub's change history. Only hashes and codes are there; no names.

## Article 6 (Companies we use, and sending information abroad)

1. We use the services of these companies. For each company we show the company's country, the country of the servers that handle the data, the legal basis for sending it abroad, and the measures the company takes (Act on the Protection of Personal Information, Article 28; Enforcement Rules, Article 17(2); the Personal Information Protection Commission's Q&A 10-24 and 10-25).

   | Company | For what | Information handled | Company's country | Servers' country | Basis (proposal) | Measures the company takes |
   |---|---|---|---|---|---|---|
   | Anthropic PBC | The support bot, and staff reply drafts and translations (both planned; drafts only when a staff member presses the button); reply drafts in the ban console (only when the author presses the button) | Text of conversations with the bot. For drafts, the text (with friend codes, email addresses, URLs and long numbers hidden), kind, language and code of that support request (3.1, item 11). For the ban console's drafts, the reply's kind, language, ban number, rule category, dates and template (names and a shared ban line's number as placeholders); the box for pasting request text is not shown until the basis is settled (3.5, item 5) | United States | United States (to be checked) | Consent (the chat's first screen), or a system meeting the standards (DPA) . Email and Discord requests are not sent until the basis is settled | A data processing agreement (DPA) is part of its terms. API content is not used for training. Deleted within 30 days (up to 2 years for content judged to break its rules; safety scores up to 7 years) |
   | Cloudflare, Inc. | Entry to the home server (hides the home IP address), bot check | Traffic content (between the encryption end points), IP addresses, browser information | United States | Cannot be identified (traffic is handled at whichever of its locations around the world is near the visitor) | A system meeting the standards (DPA), or consent  | Publishes a DPA and security measures (to be checked) |
   | Resend (email-sending company) | Emails for sign-in links, reply notices and account notices (planned) | Email address, email content (no support message text) | United States | United States (to be checked) | A system meeting the standards (DPA); we do not use a company without a DPA | We sign a data processing agreement (DPA), checked before we contract. Its published privacy policy and security measures (to be checked) |
   | Discord Inc. | Community, tickets, notices to staff, sign-in with Discord (planned) | Names and messages in Discord. Notices contain only number, kind and language. For sign-in, the Discord user ID and name | United States | Cannot be identified (to be checked) |  (check separately the parts where users use Discord themselves and the parts we send) | Its published privacy policy. No DPA with us |
   | Ticket Tool (the Discord ticket bot) | Discord tickets | What is written in tickets | To be checked | To be checked |  | Set not to keep transcripts |
   | GitHub, Inc. | Site, downloads, definitions file, central lists, documents such as the terms | Visitors' IP addresses, list contents | United States | United States and others (to be checked) |  | Its published privacy policy and security measures (to be checked) |
   | Google LLC (Gmail) | Support email | Email content | United States | Cannot be identified (to be checked) | A personal account with no DPA, so rely on consent or move to a service with a DPA   | Its published privacy policy |

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
   2. When we rely on consent, before you start we show the following on screen and ask for your consent (Enforcement Rules, Article 17(2)):
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
3. Publishing the central lists is described in Article 8.
4. Official reports to Innersloth are sent by PocketRoles on the host's PC (3.9). They are not sent from us.

## Article 8 (Lists we publish)

1. The shared-ban list, the erase list and the lift list (planned) are put in the definitions file and can be read by anyone on GitHub. This is so they reach every host's PC.
2. Only these are on them:
   - Shared-ban list: PUID hash (a value that cannot be turned back into the original number), level, end date, short reason word
   - Erase list: erase codes and dates
 - Lift list (planned): evidence number and date
   - Names, friend codes and IP addresses are not on them.
3. Even so, please note:
   1. Hosts using PocketRoles can make the PUID hash of people who join. So a host can find out whether someone who joined is on the shared-ban list. This is how shared bans work.
   2. Anyone who knows your code can see that the code is on the erase list.
   3. If you type `/cmd id` in an unregistered room, everyone in the room sees the code. If you can, type it in a registered room.
 4. When we make the lift list, we will use a form that does not let others see that the person was restricted before.
   5. PocketRoles also uses the same list to check whether your own account signed in on that PC is on it (only inside that PC; 3.11).
4.  The author can sometimes connect a value on a list with a person, through support messages or report zips. So publishing the lists may count as "giving information to others" under the law. We will check this before publishing.

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
7. If the host of the room you are in gets a shared ban while the room is open, that host's PocketRoles leaves the room (Play Rules, Article 9(6)).
   - When no match is being played, it leaves at once. During a match, if the reason is one of the certain cheat detections (killrole, ventrole, abilityrole or taskimpostor in Play Rules, Article 9(5)), or cheat (what the author decided was cheating after looking at the records), it leaves at once even mid-match; for any other reason (chat, harass, name, other: chat, bothering others and so on), it leaves after that match ends. A reason word we do not know also waits for the match to end.
   - The people in the room are not told about the ban or its reason (in the lobby it only says "The host is leaving this room"). When the host leaves, Among Us's own "… left the game" notice still appears.
   - No new information is collected or sent for this.
8. To have your records erased, see Article 12. You do not need to ask the host.
9. How these records are handled is up to the host on their own PC. We have set the defaults to "delete automatically (Aegis evidence records after 90 days, logs and the like after 30 days)" and "do not keep IP addresses".
10.  Innersloth's Terms of Use ask users not to collect or store other people's personal information from the game without their clear permission (section 11(c)). We will check how Aegis keeping records on host PCs relates to this.

## Article 10 (Children's information)

1. Many people who use our Services are children. So we do the following.
   1. We collect as little as we can (email is optional, replies are read through a secret link, an account is optional, and so on).
   2. When consent is needed for information about someone under 16, we get it from a parent or guardian.
   3. People under 13 cannot use Discord. Please use email or the support chat.
 4. Before a support chat starts, we ask "Are you under 16?". If the answer is yes:
      - we ask them to use it together with a parent or guardian;
      - the bot is not used; staff answer, and AI is not used for reply drafts either;
      - no email field is shown.
   5. When someone under 18 makes an account, we ask them to confirm "together with my parent or guardian". We also ask "Are you under 16?".
   6. Parents and guardians can ask to check or delete their child's support requests, account and records (Article 12).
   7. Where something still relies on consent for someone under 16 (such as sending information abroad), we take steps to check with a parent or guardian (for example, a confirmation link sent to the parent's or guardian's email; once confirmed, that email address is deleted). As far as we can, we arrange things on bases that do not rely on consent (such as a system meeting the standards in Article 6(3)), so that a child's consent is not needed.
2. People under 16 are expected to become able to ask us to stop using their information even when no law was broken (a 2026 amendment of the law, expected to start by 2028). When it starts, we will update this article.
3. Under Chinese law, information about children under 14 is especially sensitive information.
4. The same amendment is expected to require that minors' information be handled putting what is best for them (their best interests) first (Article 40-2). We handle it that way from now on.

## Article 11 (What we do to keep information safe)

1. **Rules and structure**
   - A responsible person is set (the author).
   - For now, only the author can read the text of support chat messages (Article 2(4)).
   - We record which staff member did what and when.
2. **People**
   - Admins promise to keep things confidential.
   - We explain to admins how to handle information.
3. **Physical**
   - The home server is inside a home.
   - Stored message text is encrypted. The key is locked inside a part of the PC (the TPM). Taking the disk alone does not let anyone read it.
4. **Network**
   - To keep the home IP address hidden, we use Cloudflare Tunnel. No port is opened at home.
   - The staff page can also be opened from the internet. Only people who sign in with Discord and are on the list of IDs we have set can enter (today that is the author alone). If it is extended to admins, only people who pass two-step verification (such as a passkey) will be able to enter.
   - IP addresses are not written to logs.
   - URLs that are sent to us are not opened.
 - Emails are not sent directly from the home server; they go through an email-sending company (so the home IP address is not left in the email).
5. **Foreign environments**
   - The countries of the companies we use, the countries of the servers that handle the data, and the measures we take in light of those countries' systems are in the table in Article 6 (United States, Germany; where the servers' country cannot be identified, that fact and the reason).
   - The home server is in Japan.
6. Things that would weaken security if written are not written here.

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
      2. Send that code to the contact in Article 14 (write "Erase records" in the subject or the first line).
      3. The author puts the code on the erase list. Each host's PC erases your records the next time it gets the definitions file (at game start, and once a day while running).
   2. **To delete a support request**: press "Delete" on the support chat screen (the secret link).
   3. **To delete an account**: press "Delete account" on the account page. It is deleted right away (and from backups within 3 days).
   4. **To see your information**: besides the code, we ask a few questions to confirm it is you (when you joined, what in-game name you used and so on). This is so we do not show it to someone else. For account information, sign in first and then ask. You can choose how we show it (for example, as electronic data by email).
   5. **From a parent or guardian**: ask with your child's code, a support request written together with your child, or from your child's account.
3. What is erased (with a `/cmd id` code)
   - names, history (room codes where entry was stopped and so on)
   - evidence records and the log lines backing them (without waiting for the 90 days; for records newer than 30 days, the name, room code, log lines and detection text are removed at once and the rest is kept until 30 days)
   - copies held by the author and admins
4. What stays
   - For a **restriction that is still in effect**, the minimum needed to keep it working (hash, level, end date) and the evidence record for that restriction. If your erase request is still listed when the restriction ends, they go at once (a record younger than 30 days is emptied at once and deleted at 30 days). If it is not listed any more, they stay until the later of 30 days after the restriction ends and 90 days after the record was made (kept for good if it has no end).
   - **The count of violations** (PUID hash, count, dates). 1 year after the last restriction ends.
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
   - after the 2026 amendment of the law starts, when information was given to others in a way the law does not allow (before publishing, we will check that the way the central lists are published is not such a case; Article 8(4))
3. For people we have no way to contact, we announce it on the site.

## Article 14 (Questions and complaints)

- Current contacts
  - Email `pocketroles.report+help@gmail.com` (please start the subject with "Personal information")
  - A ticket on the Discord server "PocketRoles 役職部屋"
- Future contact: the support chat opened from "Contact us" on the site (planned)
- Japanese, Chinese or English are all fine.
- We are not a member of an "accredited personal information protection organisation".

## Article 15 (What we show at installation)

1. The first time StarPocket Client opens, before it installs or sends anything, it shows these screens in this order:
   1. "Terms of Use and Privacy Policy": a summary and the full texts, the versions and dates, a "Use chat translation" checkbox (**unticked at first**; nothing is sent unless you tick it), an "Automatically report players removed for cheating to Innersloth" checkbox (**unticked at first**; nobody is reported automatically unless you tick it), an "I have read and agree to the Terms of Use and the Privacy Policy" checkbox (unticked at first), and "Agree"
   2. About Aegis: what it checks, what it does not do (no driver, no restart, PC scan results not sent), and "Install"
   3. Installation: progress, with a one-line explanation of what is happening now
2. Changes to PC settings (starting the Client when the PC starts, making a desktop shortcut and so on) are made only when you choose them.
3. There is a way to uninstall (settings, "General" → "Uninstall"). You can also uninstall from the first screen, without agreeing. If you do not agree, the official Client closes, and it sends nothing (Terms of Use Article 3(3)). The first time you open it, nothing is kept. If you do not agree after the terms change, what is already installed (the mod copy, settings, the earlier consent record, logs) stays as it is. To remove it, press "Uninstall".
4. If we add something new that sends information, the Client asks you again.
5. Code signing (planned): "Free code signing provided by SignPath.io, certificate by SignPath Foundation". Details will be on the site's "Code signing policy" page.

## Article 16 (Cookies and similar)

1. Pages of the site that you only read use no cookies. The browser stores only your language and display choices (3.6).
2. The support chat and account pages (planned) use the following. All are only what is needed to make the service work.
   - Cloudflare Turnstile (bot check)
   - the secret link token that lets you read replies (only in that browser)
   - a sign-in cookie while signed in (30 days) and a device mark (Article 5)
3. On a PC shared with family, links stay in the browser history. Sign out when you finish.

## Article 17 (Changes to this policy)

1. This policy always shows a version number and the date it takes effect. Each time we change it, we add to "Change history" on the site the version, the date, a summary of the change and the differences from the previous version. Older versions can be read there too.
2. When we change this policy, we announce what changes and when, in advance, on the site, in StarPocket Client's news and on Discord.
3. For important changes, such as sending more information or to more places, or using information for more purposes, we ask for your consent again in StarPocket Client, the StarPocket Account and the support chat.
   - If we change a purpose so much that it can no longer be said to be related to the previous purpose, we get the person's consent before using it that way (Act on the Protection of Personal Information, Articles 17 and 18).
   - When we send information abroad based on consent, we ask for consent for each new recipient (Article 28).
   - For people who have not agreed, we do not start the new sending or the new use.
4. Corrections that do not change the meaning, such as typos, are only added to the change history.
5. The correct text of this policy is only the text on the official site, in the official GitHub repository, and inside the official StarPocket Client (signed by us).

---

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
