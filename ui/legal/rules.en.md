# PocketRoles Play Rules (Aegis Rules)

- Version: 1.0
- Last edited: 2026-09-26
- Starts on (takes effect): 2026-09-26
- Made by: StarPocket Games (the activity name of one hobby developer; not a company)

> The Japanese version is the original.

---

## The short version

- Don't cheat.
- Don't say mean things.
- Don't use cheat tools.
- If you think we got it wrong, contact us.

Every article has a number. Aegis and the error-code screens show a number like "Play Rules, Article 3". You can read that article here.

---

## Article 1 (About these rules)

1. These rules are for rooms that use PocketRoles, so that everyone can play fairly and have fun.
   - Aegis is PocketRoles' anti-cheat. Aegis works from these rules.
2. Three kinds of people are involved.
   1. **Hosts** (people who put PocketRoles on their own PC and make rooms)
      - Like everyone else, hosts follow Articles 2 to 6. On top of that, they follow Articles 3 and 4 for their own PC, and Article 7 for their own room.
      - While a shared ban (Article 9) lasts, that Among Us account can't create rooms with PocketRoles (Article 9(6)).
   2. **People who join a room**
      - You can join without installing anything. Joining does not mean you agreed to anything.
      - When you join, Aegis watches what you do, using these rules, which the host of that room uses.
      - If something does not fit the rules, you may be removed from that room or not let back in.
      - This is the host deciding who can be in their own room. It is not a ban of your Among Us account.
      - Unless the host turns it off, the shared-ban list (Article 9), which the author decides after looking at evidence, is used automatically. This is not a contract; it is our decision and the host's. You can appeal (Article 10).
      - These rules are public so anyone can read them.
   3. **People who use StarPocket Games' services** (StarPocket Client, the site's support, Discord, shared-ban requests and so on)
      - For them, these rules are part of the Terms of Use.
3. A host can show extra rules for their own room with `/rules`. Removing someone because of the host's own rules is that host's decision (Article 6).
4. Please also follow the rules of Among Us itself (Innersloth's Terms of Use). These rules add to those.
5. PocketRoles is GPL-3.0 software.
   - These rules do not limit your right to use, modify or share the software.
   - These rules only cover how people behave in PocketRoles rooms and how StarPocket Games' services are used.
6. Article numbers appear in StarPocket Client, in PocketRoles (inside the game) and on the site's error-code pages. For codes starting with R, J or B, the first digit is the article number of these rules (R-31 is Play Rules, Article 3; B-91 is Play Rules, Article 9). Codes starting with S are about Terms of Use Article 13. Codes starting with P, N or A have nothing to do with these rules.

## Article 2 (No cheating in the game)

1. Do not do things that normal Among Us cannot do. For example:
   1. Killing with a role that cannot kill. Killing while dead. Killing during a meeting or the exile screen.
   2. Entering a vent with a role that cannot vent. Entering a vent from far away.
   3. Using an ability your role does not have (shapeshifting, turning invisible and so on).
   4. Completing a task as an impostor. Completing a task you do not have. Finishing tasks impossibly fast.
   5. Moving much faster than the speed setting (speed hack). Teleporting.
   6. Making an impossible report (the body of someone who is alive, or reporting after you died).
   7. Sending messages that normal Among Us never sends.
   8. Chatting outside a meeting while alive.
   9. Trying to change your name inside the room. Trying to change colour during a game. Changing colours faster than a hand can.
   10. Naming or voting for impostors who have not done anything yet, game after game (a sign of a cheat that shows impostors).
2. Aegis sorts these into 3 levels (details in Article 8).

   | Level | Which actions | What Aegis does |
   |---|---|---|
   | Certain | Item 1 "kill with a role that cannot kill", item 2 "vent with a role that cannot vent", item 3, and item 4 "task completed by an impostor" | Removes the player at once and restricts them from the room. Also reports them to Innersloth (unless the host turned automatic reporting off). During a match in an unregistered room, it may end that match (Article 8(2)(4)) |
   | Repeat | Item 5 speed hack, item 8, and chat flooding in Article 5(2) | The first time is only recorded. A second time, in a different moment, removes the player |
   | Notice | Everything else | Only shown on the host's screen. This alone never removes anyone |

3. Actions that lag (network delay) can also cause are "Notice" only.
4. The numbers used for these checks are published in the definitions file. Each room changes them a little (up to ±10%), so cheaters cannot aim for the gaps.
5. In registered rooms, the game-action checks (item 1 to 6 and so on) are not run. Messages that normal Among Us never sends (item 7), chat flooding, and name and colour changes (item 9) are checked in both kinds of rooms.

> Related codes: J-21 (Certain), J-22 (Repeat)

## Article 3 (No cheat tools)

1. Do not use cheat tools in PocketRoles rooms. This is the same for hosts and for people who join.
   - For example: memory editors, trainers, DLL injectors, cheat menus, modified copies of Among Us.
2. On the host's PC, keep tools that can be used for cheating closed while PocketRoles is running.
   - This includes tools you use for other games. While one is running, the Play button in StarPocket Client cannot be used.
   - The names of the tools we look for are in the `[tools]` section of the definitions file.
3. On the host's PC, do not put DLLs used by cheats in the game folder.
   - DLLs with cheat, menu or inject in their name, and DLLs with the names of known cheats (R-32)
   - "Swap-in" DLLs placed next to Among Us.exe (`version.dll`, `dxgi.dll` and so on) (R-33)
 - Tools that change how the screen looks (such as ReShade or DXVK) also use the R-33 names, so they may stop the game from starting. If that happens, remove them before you play.
4. Do not change the game's code while it is running (R-34).
5. If something in this article is found, the Play button stops and shows the reason and a code. **Close it or remove it and you can play right away.**

> Related codes: R-31 (tool), R-32 (cheat-named DLL), R-33 (swap-in DLL), R-34 (changed while running)

## Article 4 (What the host's PC needs)

1. To make PocketRoles lobbies and play on the host's PC, the PC needs the following. StarPocket Client (and today's launcher) checks before playing; PocketRoles inside the game (its self-check) checks before making a lobby.
   1. PocketRoles.dll has not been changed since it was put on this PC (its contents have not changed while the version number stayed the same) (R-41)
   2. BepInEx's `plugins` folder has no plugin other than PocketRoles.dll (R-42). The `patchers` folder is empty too (R-43)
   3. BepInEx starts with the start-up part of the set version (6.0.0-be.735) (R-44)
   4. Windows' protection (such as driver signature checks) is not weakened by test mode, debug mode or turned-off integrity checks (R-45)

   Items 2 and 3 are also checked by PocketRoles inside the game. If they are not met, the next lobby cannot be made.
2. This protects the people who join. It also lets Aegis check things properly.
3. The GPL lets you modify PocketRoles yourself. This article does not forbid modifying it.
   - If you use a modified version, do not say it is the official version.
   - If you use StarPocket Games' services (such as shared-ban requests) with a modified version, tell us it is modified.
   - You can use a version you built yourself in developer mode. In developer mode, item 1(1) (whether the files match the official version) does not stop you; it only shows a notice (R-41). In normal mode it still stops you as before.
4. If this article is not met, the reason and a code are shown. **Fix it and you can play right away.**
   - For items 1 and 3 (a file has changed or is different), the Play button turns into "Repair". Press it to reinstall; when it finishes, it turns back into "Play".
   - Some people turn on item 4 (test mode and so on) themselves for development. This is not proof of cheating either. Turn it off and restart the PC, then you can play.

> Related codes: R-41, R-42, R-43, R-44, R-45

## Article 5 (Chat rules)

1. Do not use hurtful words.
   - For example: words telling someone to die; the same words put after a colour name (players call each other by colour in this game); discriminatory words; insults about how someone looks; sexual words.
   - There are word lists in Japanese, Chinese, English and Korean (the `[ngwords]` section of the definitions file; the list is no longer printed as it is and cannot be read at a glance).
2. Do not flood the chat faster than a person can type (about 5 lines in 3 seconds).
3. Do not write other people's personal information (real name, address, school, social media accounts, photos and so on).
4. Do not pretend to be someone else when you talk.
5. What Aegis does
   - Words in item 1: the first and second time, a warning everyone in the room can see. The third time, the player is removed and cannot come back to that room.
     - Words that come within 5 seconds of each other count as one time.
     - If 3 people are hit within 2 minutes, that room stops removing people for a while (to avoid mistakes).
     - The host, VIPs, moderators and admins get no automatic warnings. The rules are the same for them.
   - Flooding in item 2: this is the "Repeat" level (Article 2(2)).
   - Items 3 and 4: Aegis does not detect these. The host watches and decides (Article 6).
6. Normal conversation is not kept in Aegis's records.

> Related codes: J-51 (hurtful words), J-52 (flooding)

## Article 6 (Don't bother other people)

1. Do not do the following.
   1. Picking on the same person again and again. Following someone around.
   2. Threatening other players or the host.
   3. Joining with a name that uses discriminatory or sexual words, or pretends to be someone else.
   4. Continuing to break the room rules (`/rules`) after being warned.
   5. Leaving and rejoining again and again to stop the game on purpose.
2. The host, not Aegis, watches for these and decides.
   - The host can remove the person or stop them from joining the host's room (a host ban).
3. Whether to make it a shared ban (Article 9) is decided by the author after looking at the evidence.

> Related code: J-61

## Article 7 (Rules for hosts)

1. Hosts do the following.
   1. Follow Innersloth's Terms of Use and Mod Policy.
      - On the official servers, register the lobby (+25).
      - Do not make money from rooms or games (this is also Innersloth's rule). The author pays the running costs. We plan to put one optional way to help with them on the official site, and nowhere else (there is no such link yet). It has nothing to do with rooms: supporting changes nothing about how any room is treated (Terms of Use, Article 7).
   2. If the room has its own rules, show them with `/rules`.
   3. Do not change Aegis records, report zips or "One player's evidence" zips. Write only what is true in shared-ban requests.
   4. Do not post records of people who joined (names, evidence, chat) on social media or other public places.
      - Send report zips and "One player's evidence" zips (Privacy Policy 3.3) by email. Do not post them in public places.
   5. Do not ask for a shared ban just because you dislike someone.
   6. If you use chat translation, understand that the chat of people in the room is sent to a translation company (Google or DeepL). It is off by default; turn it on only if you understand this. That is for a fresh install: **the config file of someone upgrading from v0.5.4 or earlier stays on.** v0.5.5 tells that host once and hands them `/opt upgrade off`. Type `/opt translate off` in chat if you do not want it.
      - When translation is on, the welcome line says "Auto-translation is on" (not if the host changed or turned off the welcome line).
 - DeepL's free key is meant to be used without sending personal information. If you use a free key, think about whether you can keep that promise.
   7. Do not say "Aegis protects this room" in a room where Aegis detection or shared bans are turned off.
2. If a host used a cheat tool in a room (Article 3(1)), said a modified version was official or did not tell us it was modified when using the services (Article 4(3)), or broke this article, we may stop some of StarPocket Games' services for them (Terms of Use Article 13).
   - Not meeting the conditions for the state of the host's PC (Article 3(2) to (4) and Article 4(1); R codes) is never, on its own, a reason to stop anything.
   - What we may stop: the room list, shared-ban requests, the "Host" role on Discord and so on.
   - Even when we stop a service under this item, we do not stop anyone using PocketRoles on their own PC (GPL). We do not stop anyone playing Among Us either.
   - A shared ban (Article 9) is separate from the service limits in this item. While a shared ban lasts, that account can't create rooms with PocketRoles, as in Article 9(6).
   - We tell them about a stopped service on Discord, by email or on that feature's screen. When the room-list key is stopped (S-12), it is also shown in StarPocket Client's "Service status" area, the same place as maintenance notices. Stopping a service never stops the Play button.
3. If a host's shared-ban requests (reports) are found to be wrong on appeal 3 times within 30 days, we stop accepting their requests, even if they were not lies (S-11; Article 9(2)).
   - Only reports that were not "Certain" detections count ("Repeat" detections, hurtful words or flooding in chat, harassment, names, and things the host decided by watching).
   - When a "Certain" detection was wrong, it was Aegis's mistake, not the host's, so it does not count (we fix Aegis's checks instead).
 - Only the last 30 days count (the same length as keeping the record used for counting, `reporters.txt`). Once there are fewer than 3 in the last 30 days, we accept requests again (usually 30 days after the oldest of the 3).

> Related codes: S-11 to S-16 (Terms of Use Article 13)

## Article 8 (What Aegis does, and the levels of room restriction)

1. Aegis has 3 levels: "Certain", "Repeat" and "Notice" (Article 2(2)).
2. For "Certain"
   1. The player is removed at once.
   2. The player is restricted in that host PC's list. The length is 30 days the first time, 180 days the second time, and no end from the third time.
      - The count goes back to 0 one year after the last restriction ends.
   3. An official Among Us report is sent to Innersloth (only when the host turned automatic reporting on; it is off by default; at most once every 30 days for the same player). Innersloth decides what to do with reports.
   4. **Stopping the match**: during a match in an unregistered room, if the cheat really changed the match (a kill landed, someone went into a vent, or someone used a shapeshift or vanish they don't have), that match is ended for everyone.
      - The cheater is removed and restricted, as in 1 to 3.
      - After everyone is back in the lobby, a message without a name is shown: "[Aegis] Cheat found: match ended, player removed. Ignore that win/lose screen."
      - A match is stopped at most once per match and 3 times an hour (this still counts if the room is made again). Past that, the match is not stopped and goes on. The cheater is still removed and restricted as usual, and the message with the name in (5) is shown.
      - When nothing in the match changed, such as an impostor finishing a task, only that player is removed. The match goes on.
      - Matches are not stopped in registered rooms or for people who are VIP or higher. The host can turn this off in settings (it is on by default). If the match cannot be ended properly, it simply goes on, and the message with the name in (5) is shown.
3. For "Repeat"
   - If it happens a second time in a different moment (10 seconds or more apart), the player is removed and cannot come back to that room.
4. For "Notice"
   - It is only shown on the host's screen.
5. When a player is removed, everyone in the room sees "[Aegis] Someone was removed: impossible action (rule name)". By default the message to everyone does not include the name; the name is shown only on the host's screen. A host can turn the name back on in the settings.
   - When the match is stopped (2(4)), only the message without a name is shown instead. However, Among Us's own notices may still show the name.
6. When a restricted player joins a room:
   - They get a message and are removed after about 30 seconds.
   - They are removed at once on an Aegis detection, a hurtful word, flooding, a 4th chat line, or when the game starts.
   - If they join the same room again, they are removed at once and won't be able to join it again.
   - People the host made VIP or higher are not removed by a restriction.
7. When Aegis removes or restricts someone, it keeps a record (evidence) on that host's PC.
   - What is recorded: time, room, rule, detection numbers and so on
   - What is not recorded: IP addresses, other people's chat
   - How records are handled is written in the Privacy Policy.
8. Only that host can lift a restriction on the host's PC. The exception is the lift list in Article 10(4) (planned).

> Related codes: J-21, J-22, J-81

## Article 9 (Shared bans)

1. A shared ban is a restriction that keeps a player out of PocketRoles rooms (v0.5.5 or later) that use shared bans. Most hosts have it on, as it is by default. Also, while a shared ban lasts, that Among Us account can't create rooms with PocketRoles (item 6; planned).
2. Only the author can make a shared ban.
   - The author decides after looking at Aegis records (evidence). A host's request alone is not enough.
   - When it is based only on records sent by other hosts, these rules apply.
     1. A "Certain" detection whose record matches that host's game log: one host's record is enough, but only for the first level (up to 30 days). At most 2 shared bans per 30 days may be decided from one and the same host's records alone.
     2. Anything else (a "Repeat" detection such as a speed hack, a restriction for chat (hurtful words, flooding) or harassment, or one the host decided by watching): it needs records from 2 or more different hosts, received within 30 days of each other, or a report from a trusted admin. Otherwise, the author must write down the reason and keep it in the log of actions on the author's PC (`audit.log`). This "2 or more different hosts" may go up to a larger number as more hosts join.
     3. In both cases, it lasts at most 30 days.
   - If a host's reports are found to be wrong on appeal 3 times within 30 days, we stop accepting reports from that host (Article 7(3); S-11 in Terms of Use Article 13).
   - These numbers are public. The three below count different things, so they are written apart to keep them from being mixed up.
     - **How many hosts are needed** (a count of hosts): one host for a "Certain" detection; **2 or more different hosts** within 30 days for anything else.
     - **How many shared bans one and the same host alone can bring** (a count of bans): at most **2 per 30 days**. This cap is only for "Certain" detections, and it is not a count of hosts.
     - **How many mistakes stop a host's reports** (a count of mistakes): reports are not accepted from a host whose reports were found to be wrong on appeal **3 times within 30 days**. This is not a count of hosts either.
   - "How many hosts are needed" is 2 because there are still few hosts: with 3, three would rarely come together and almost no shared ban could be made. As more hosts join, this number may go up. We will say so beforehand when it changes.
3. The shared-ban list is public. Only these 4 things are on it:
   - a number changed into a form that cannot be turned back (a hash of the PUID)
   - the level
   - the end date
   - a short word for the reason

   Names and friend codes are not on it.
4. When the end date comes, it is lifted automatically.
5. Reason words and the related articles

   | Reason word | Meaning | Article |
   |---|---|---|
   | killrole | Killing with a role that cannot kill (a certain detection) | Article 2 |
   | ventrole | Using a vent with a role that cannot vent (a certain detection) | Article 2 |
   | abilityrole | Using an ability a role does not have (a certain detection) | Article 2 |
   | taskimpostor | An impostor finishing tasks (a certain detection) | Article 2 |
   | cheat | Other cheats (the author picks this) | Articles 2 and 3 |
   | chat | Hurtful words, flooding | Article 5 |
   | harass | Harassment, threats | Article 6 |
   | name | Bad names | Article 6 |
   | other | None of the above (the author records the detailed reason and article) | The article in the record |

   A shared ban made from a certain detection carries that detection's own name. For anything else the author picks one of `cheat`, `chat`, `harass`, `name` and `other` (a speed hack is found by repetition, so the word `speedhack` never appears on a shared-ban line: it becomes `cheat` or `other`).

6. While a shared ban lasts, that Among Us account can't create rooms with PocketRoles (B-91).
   - PocketRoles inside the game does the check. It compares the Among Us account that is signed in with the shared-ban list, inside this PC. **Nothing is sent.**
   - What stops is creating online rooms (public and private). Local rooms, played on the same Wi-Fi, can still be created (decided 2026-09-23). If that turns out to be a problem in places where people gather, the author can stop local rooms too by writing `[selfban] local = block` in the signed definitions file (no new version of the mod is needed).
   - If the shared ban starts while you are hosting a room, PocketRoles leaves that room. The players are not told about the ban or its reason (in the lobby they only see "The host is leaving this room").
     - No match running: it leaves right away.
     - During a match, when the reason is one of the certain cheat detections (killrole, ventrole, abilityrole or taskimpostor in item 5; Article 2): it leaves right away, even mid-match. A match hosted by someone who cheats cannot go on fairly.
     - During a match, for any other reason (cheat, chat, harass, name or other in item 5: cheating the author decided from the records, chat, bothering others and so on) or a word we do not know: the match is played to the end, and it leaves once back in the lobby, so the other players' match is not wrecked. No new match can start until it has left.
     - After the host leaves, one of the remaining players usually becomes the new host and the room goes on without PocketRoles' roles (when it leaves mid-match, whether that match goes on or ends depends on Among Us).
   - What does not stop: joining other people's rooms (but, as in item 1, not rooms that use shared bans), Practice, and plain Among Us from Steam.
   - It is decided per account. Other accounts on the same PC are not affected.
   - The main menu of PocketRoles in the game, StarPocket Client, today's launcher and the tray Aegis show "Your access to this game is restricted". With it, they show the end date, the reason (the word and article in item 5), how to appeal, and **your code** (the same 16 letters as `/cmd id`) (in StarPocket Client, after pressing "Restricted"). You don't need to ask anyone for the code; you can use it for an appeal as it is (Article 10).
   - The Play button in StarPocket Client turns into a grey "Restricted" button. Pressing it does not start the game; it shows the reason, the period, your code and how to appeal. "Play plain Among Us (Steam)" is shown too.
   - When the end date comes, it is lifted right away. When an appeal is accepted and the line is removed from the list, it is lifted the next time PocketRoles receives the definitions file (it also fetches the file again when you try to create a room). In both cases, you don't need to restart the game.
   - This is a PocketRoles shared ban. It is not a ban of your Among Us account.

> Related codes: J-91, B-91

## Article 10 (If you think it's a mistake: appeals)

1. If you think you were restricted by mistake (a false detection), you can appeal.
   - Once per restriction. An appeal we find was made by someone else does not count as that one time.
2. Where to appeal
   - A ticket in "🛡️｜異議申し立て" (Appeals) on the Discord server "PocketRoles 役職部屋"
   - Email `pocketroles.report+help@gmail.com` (subject "Appeal")
   - The support chat on the site (after the home server is ready; planned)
3. What to write
   1. Your in-game name when you were restricted (please don't write your real name)
   2. The date and time, and the room code (as far as you know)
   3. Your friend code (used only to match against the records, and deleted within the period in Privacy Policy Article 5)
   4. Why you think it is a mistake
   5. The code (such as J-21) if one was shown on your screen
   6. If "Your access to this game is restricted" (B-91) is shown: "Your code" on the screen (16 letters). In that case, the friend code in 3 is not needed.
      - This code is shown on the screen, so other people may see it. So when an appeal gives only the code, our reply does not include what is in the record (chat lines, the room, the date and time, and so on). We include it only when the name in 1 or the date and time in 2 match the record.
4. How we decide
   1. We decide by looking at the Aegis records. An explanation alone is not enough.
      - If the record of that restriction is only on the PC of the host who made it, the author asks that host to send only your records (a "One player's evidence" zip; Privacy Policy 3.3). You do not need to find the host. The author then receives every record of you on that host's PC (not only the restriction you appealed, but also records of a removal alone). No other player's records go in, although a detection line can name another player of that game.
      - Evidence records stay on the host's PC for 90 days. They are not deleted while the restriction lasts, and they stay 30 more days after it ends (if you asked for your records to be erased, they may go sooner). Once a record is gone, it can only be checked if the author already holds it, so please appeal early.
   2. A restriction with certain evidence is not lifted unless we find a mistake in the record.
   3. We do not lift a restriction early because someone says they are sorry.
   4. If we find it was a mistake, we lift it.
      - Lifting a shared ban reaches each host the next time they start the game.
      - The PocketRoles of the person whose shared ban was lifted can create rooms again the next time it receives the definitions file (Article 9(6)).
 - We plan a "lift list" so the author can lift automatic restrictions on host PCs.
   5. Today the author is the only staff member, so the author looks at it again. When there are more staff, a staff member other than the one who first decided will look at it again.
5. A restriction a host made themselves (a host ban) can only be lifted by that host. We can tell the host, but we cannot lift it.
6. "You can't play yet" in StarPocket Client (Articles 3 and 4; codes starting with R) tells you about something found on this PC.
   - No appeal is needed. Fix it and it goes away right away.
   - If you think something was found by mistake (a tool that only has the same name, and so on), tell us with the "Contact support" button on that screen. We will fix the definitions file.

## Article 11 (If you want your records erased)

1. Type `/cmd id` in a room's chat to get your erase code (16 letters). While a shared ban lasts, the same code is also shown on the screens in Article 9(6).
2. Send that code to the author. The author puts the code on the "erase list". Each host's PC erases your records the next time it gets the definitions file.
3. What is erased, what stays, and what to be careful about are in Article 12 of the Privacy Policy.

## Article 12 (Changes to these rules)

1. When we change these rules, we announce what changes and the day the new rules start (take effect), in advance, on the site, on Discord and in StarPocket Client's news.
2. We may change the numbers used for detection in the definitions file, within set ranges. We may make them stricter to close gaps.
   - The levels (Certain, Repeat, Notice) can only be made more lenient by the definitions file. To make a level stricter, we change these rules and announce it in advance.
3. Even if article numbers change, the meaning of an error code does not change. The error-code list shows which code belongs to which article.
4. Every version shows its version number and the day it starts (takes effect). Older versions can be read in "Change history" on the site.
5. The correct text of these rules is only the text on the official site, the official GitHub, and inside the official StarPocket Client.

---

## Appendix: articles and codes

| Article | Related codes |
|---|---|
| Article 2 No cheating in the game | J-21, J-22 |
| Article 3 No cheat tools | R-31, R-32, R-33, R-34 |
| Article 4 What the host's PC needs | R-41, R-42, R-43, R-44, R-45 |
| Article 5 Chat rules | J-51, J-52 |
| Article 6 Don't bother other people | J-61 |
| Article 7 Rules for hosts | S-11 to S-16 (Terms of Use Article 13) |
| Article 8 What Aegis does | J-21, J-22, J-81 |
| Article 9 Shared bans | J-91, B-91 |

- Codes starting with R: something found on the host's PC. Fix it and it goes away (R-41 and R-44 are fixed with "Repair" on the Play button).
- Codes starting with J: about people who joined a room. You can appeal.
- Codes starting with B: while a shared ban lasts, that account can't create PocketRoles rooms. You can appeal.
- Codes starting with S: limits on StarPocket Games' services. You can appeal.

## Change history

| Version | Date | Changes |
|---|---|---|
| 0.1 (draft) | 2026-09-22 | First draft |
| 0.2 (draft) | 2026-09-22 | Codes assigned (R-32 to R-34, R-43 to R-45, S-11 to S-16). What the definitions file can change now matches how it really works. Added the change rules and the official places |
| 0.3 (draft) | 2026-09-22 | Applied the review. Honest description of joiners and shared bans (Article 1(2)). Rewrote how codes relate to articles (Article 1(6)). Article 4 is now "What the host's PC needs", says the game checks too, and describes how R-41 is really checked. PC-state conditions alone are no longer a reason to stop services (Article 7(2)). Corrected which rooms a shared ban applies to (Article 9(1)) |
| 0.4 (draft) | 2026-09-22 | Applied the owner's decisions. Stopped saying "this is not a punishment" (Article 3(5), Article 4(4), Article 10(6)). Said that for file problems the Play button turns into "Repair" (Article 4(4)). Added stopping the match for a certain cheat (Article 2(2), Article 8(2)(4), Article 8(5)). Added that stopped services are shown in the Client's "Service status" area, and what happens to hosts whose reports were wrong again and again (Article 7(2) and (3)). Added the conditions for making a shared ban from other hosts' records (Article 9(2)). In the same version's review: said automatic reporting happens only "unless the host turned it off" (Article 2(2), Article 8(2)(3)); said what happens past the match-stop limits and when a match cannot be ended (Article 8(2)(4)); corrected that only S-12 is shown in the Client's "Service status" (Article 7(2)); said "Repeat" detections also fall under the 2-host conditions (Article 9(2)(2)) |
| 0.5 (draft) | 2026-09-22 | Applied the owner's decisions (D-35, D-36). Added that, while a shared ban lasts, that Among Us account can't create PocketRoles rooms; that PocketRoles in the game checks this inside the PC and sends nothing; the screens it appears on and code B-91; how it is lifted; and using "Your code" for an appeal (Article 1(2), Article 1(6), Article 7(2), Article 9(1), Article 9(6), Article 10(3), Article 10(4), Article 11(1), Appendix). Said that requests from a host whose reports were found to be wrong on appeal 5 times within 30 days are not accepted, and which reports count and which do not (Article 7(3), Article 9(2)). A review the same day added that an appeal made by someone else does not count as the one appeal, and that a reply to an appeal with only the code does not include what is in the record (Article 10(1), 10(3)6) |
| 0.5 (draft) | 2026-09-23 | Applied the owner's answers (the rest of D-36) (Article 9(6)): the Client's Play button becomes a grey "Restricted" button that shows the reason, the period, your code and how to appeal; what stops is online rooms (public and private), and local rooms can still be created ; if the shared ban starts while you host, PocketRoles leaves the room right away in the lobby, or after the match during a match ; in the Client, the end date, the reason and the code are shown after pressing "Restricted" |
| 0.6 (draft) | 2026-09-23 | Applied the owner's decisions (D-36 item 3, D-37, D-38). What happens when a shared ban starts while you are hosting now depends on the reason: for cheating (killrole, speedhack, cheat), PocketRoles leaves at once even mid-match; for other reasons, after that match (Article 9(6)). Corrected on the same day by a review: item 5's reason words now match what a shared-ban line really carries (killrole, ventrole, abilityrole, taskimpostor, cheat, chat, harass, name, other), and only a certain detection makes the host leave mid-match (speedhack and cheat are ); local rooms in item 6 are allowed (the author can stop them with `local = block`); Article 10(4)(1) now says the author receives every record of you on that PC. Added that, for an appeal whose record is only on a host's PC, the author asks that host for only that person's records (a "One player's evidence" zip), and that evidence records stay on the host's PC for 90 days (not deleted while the restriction lasts, and 30 more days after it ends) (Article 10(4)(1)). One-person exports must not be changed or posted either (Article 7(1)(3) and (4)). The 30 days for counting overturned reports are now said to match how long `reporters.txt` is kept (Article 7(3); evidence records now stay 90 days) |
| 0.7 (draft) | 2026-09-23 | Applied the owner's decisions (D-39, D-40, D-41, D-42). The examples of hurtful words now describe the kinds of words instead of naming any, and the line about the word lists now says only that the list is no longer printed as it is - not that it is secret (Article 5(1)). Changed the shared-ban numbers and published them: one host for "Certain" (at most 2 per 30 days from the same host), 3 or more different hosts within 30 days for anything else (was 2 or more), and reports are not accepted from a host whose reports were found to be wrong on appeal 3 times within 30 days (was 5; the owner: "five is too many, pick a number you think is right and I approve it") (Article 7(3), Article 9(2)). Said that chat translation is off by default (Article 7(1)(6)). Kept the rule that hosts must not make money from rooms, and added that the author covers the running costs only through the optional support link on the official site (Article 7(1)). The brand is spelled "StarPocket" |
| 0.8 (draft) | 2026-09-23 | Put the number of different hosts a shared ban needs back to **2 or more** (Article 9(2)(2), and the published-numbers part at the end of Article 9(2)). The "3" had been written into the drafts without ever asking the owner (the official site was not published yet at that time, so it never went out on a published page). On 2026-09-23 the owner answered: "keep 2 for now - with so few hosts, 3 would mean shared bans almost never happen; raise it later when there are more hosts" (D-39). Added one sentence saying the number may go up as more hosts join. Rewrote the published-numbers part as three separate items - **hosts** (2 or more different hosts), **bans** (2 per 30 days from the same host) and **mistakes** (wrong on appeal 3 times in 30 days) - so they cannot be confused. The bans and mistakes numbers are unchanged |
| 1.0 | 2026-09-26 | The first published version. **The rules themselves are unchanged from 0.8.** The "(draft)" marks, the lines explaining those marks and the "not published yet" wording are gone, and the date it takes effect is filled in. |
