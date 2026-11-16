# Homelab
Making my own personal cybersecurity testing lab

<h1> Linux setup </h1>

For this project i will use my laptop as a server.
I first installed Ubuntu 26.04.1 LTS through Ventoy

Next step was actually connecting to my PC and local network, using DHCP reservation.
The plan is to later add remote connection capabilities.

<h2> Network configuration </h2>

To create the connection i first found the necessary network info:

<img width="935" height="445" alt="image" src="https://github.com/user-attachments/assets/09c46a37-c3df-4a38-95ec-a469862fb3b7" />

The commands "ip a" and "ip route" were used to find my MAC adress on the "link/ether" line and my routers ip address on the "default via" line.

I then logged into the admin page of my router to execute the DHCP reservation:

<img width="580" height="335" alt="image" src="https://github.com/user-attachments/assets/e99e1753-75b6-49e5-b422-630e81887f3e" />

Once this was done the laptop/server needed to pick it up.
I used "sudo networkctl renew wlp3s0", with wlp3s0 being my interface name, to renew the configuration on the server.

In order to confirm the changes i rebooted both the server and my router, after which the connection persisted meaning the changes were applied successfully.


<h2> Storage </h2>

After checking my available storage i saw that the server only displayed 100GB of storage even though it had a 500GB SSD.
To check the storage i used the commands "df -h /" and "lsblk" to see the filesystem size and compare the partiton with "ubuntu--vg-ubuntu--lv" to the results from "df -h /"

The solution was using "lvextend" and "resize2fs" to grow my logical volume (LV) size and to grow the actual filesystem to the size of the new LV.

Re-inputing the "df -h /" command confirmed the changes:

<img width="559" height="51" alt="image" src="https://github.com/user-attachments/assets/05978e8b-caac-437e-8e3a-9fc23910a2ac" />

<h2> Cockpit setup </h2>

I wanted to have a GUI of my server so i decided to install Cockpit:

<img width="939" height="819" alt="image" src="https://github.com/user-attachments/assets/03e40b52-a79f-4058-8352-3daa9f4801eb" />

Through Cockpit i wanted to setup my firewall. I first installed the "firewalld" Cockpit required to function.

Unfortunatly i forgot to actually confirm the remote connection before confirming admin access on Cockpit and activating the firewall.

This in turn led to me getting locked out of the GUI http page but SSH still worked.

I added Cockpit to be allowed past the firewall and made sure that SSH had permanent access

<img width="513" height="266" alt="image" src="https://github.com/user-attachments/assets/7a5a58a7-9ec4-415b-bad9-28d5aac15ea5" />

To add SSH and Cockpit permanently i used:

sudo firewall-cmd --zone=public --add-service=cockpit --permanent

sudo firewall-cmd --zone=public --add-service=ssh --permanent

and then to reload and refresh the privlidges:

sudo firewall-cmd --reload

To access the Cockpit interface i simply went to the https://192.168.1.8:9090 URL.

<h1> Tailscale and Tailnet setup </h1>

To truly make my server a server I decided to actually make it remotely accessible.
The easiest way to do so was to use Tailscale. 

On the actual server i simply curled the install script and started the server:

<img width="926" height="835" alt="image" src="https://github.com/user-attachments/assets/7932f0a1-b441-400a-addb-6e35d8125204" />

<img width="432" height="111" alt="image" src="https://github.com/user-attachments/assets/b7cfc4af-0e08-4869-bc21-1a7c40f602e0" />

Using "tailscale status" i can get a look at every device currently in the network.

I connected my PC, server, phone and laptop into the network.
Also tailscale allows using names instead of IP adresses making the actual command line sending alot easier.

<img width="924" height="78" alt="image" src="https://github.com/user-attachments/assets/16df2e41-e13d-4d3c-a1ed-782f6201fae5" />

In order to test the connection i sent over an Excel file.
"Trening.xlsx" was sent from my PC to the server and then to my laptop while it was on a different network.

<img width="928" height="55" alt="image" src="https://github.com/user-attachments/assets/9488e757-8301-4039-95b2-f354da089bb4" />

The send command from windows is "scp <file_name> smustac@stipinlab:<server_dir>"

Then it arrived on the server and from there it was sent to the laptop using "sudo tailscale file cp <filename> <target_ip>"

<img width="569" height="56" alt="image" src="https://github.com/user-attachments/assets/1592785e-1789-4a7b-9369-03a423457cd7" />

The file was retrieved using "tailscale file get . " and appeared in the downloads folder.

I also tested all of the other paths but didnt feel the need to explain all of them since the concept is the same.
Everything arrived properly proving that the server had perfect remote functionality.

Later in the project i changed the actual Tailscale account to a new gmail that was made specifically for the Tailnet just so whenever i add another user into my Tailnet i dont actually give them my entire google login. That kinda defeates the whole security idea.


<h1> Attacks </h1>

Here i will go over any current and future attacks that i attempt on the server.
All attacks will be directed from a kali VM on my local PC.
The point of the attacks is to serve as an actual test of the setup, whether they succeed or not if they are properly executed the test is valid and so are the results.

<h2> Recon - SSH </h2>

I first nampped the server:

<img width="626" height="207" alt="image" src="https://github.com/user-attachments/assets/edb37b4a-b711-4d8e-80c0-9687ff05ee2c" />

The specific parameters in this nmap command instruct to: detect service/version, to scan port22 where SSH is located, two NSE scripts used for deeper probing and analysis.

The SSH service returned as filtered which meant the firewall was blocking it.

After troubleshooting for a while i realized the problem was the actual VM configuration. 
It had a Host-Only network adapter and a NAT network adapter but no Bridged Adapter.

The bridged adapter was necessarry because i needed the VM to use my PCs network as its own so it could se the server.

After shutting down the VM and changing the settings i retried the attack.

<img width="626" height="828" alt="image" src="https://github.com/user-attachments/assets/62d8ee03-27a4-45fa-9441-8e6d969fb9d8" />

I then just ran a basic searchsploit command, this didnt provide any results as expected of a very current SSH version.

<img width="372" height="80" alt="image" src="https://github.com/user-attachments/assets/62ab7f6a-0319-4e04-a29f-9e8c04afd9de" />

<h3> SSH Brute-force </h3>

I used the hydra tool on the Kali Vm to brute-force the server with rockyou.txt

The exact attack line was:   hydra -l smustac -P /usr/share/wordlists/rockyou.txt -t 4 ssh://192.168.1.8

Setting the username as "smustac", specifying the .txt file we want to pull from and limiting to 4 parallel connections with "-t 4".

<img width="1905" height="979" alt="image" src="https://github.com/user-attachments/assets/8de43c6f-36e8-47df-9735-ea74917862c9" />

A properly set password proved to be too complex for this attack, which is exactly what was desired. 

<h3> Defensive measure - SSh Brute-force </h3>

Even though the attack was unsuccessfull its still not desired for these types of attacks to be allowed on the server.

So i decided to install fail2ban but this once again shutdown my SSH connection to the server.
This could have happened either because there was a failed login attempt from my PC at some point which caused fail2ban to ban it or Kali at some point used a the same IP as my PC while attacking, which was then later given to my PC and the fail2ban ban applied to it.

The solution to ensure testing integritiy is to use DHCP reservation on Kali so it only ever gets the selected IP and doesnt cause these "bleed-over" bans.

First i couldnt figure out why my bridged adapter wasnt taking on the reserved IP but it turns out i just had it set to the wrong adapter on my actual PC.

After that it worked exactly as wanted.

<img width="497" height="66" alt="image" src="https://github.com/user-attachments/assets/33be4243-6b43-4939-b5eb-66ded7d6facd" />

To test i once again began the brute-force attack and the IP assigned to the Kali VM was succeesfully banned.

<img width="534" height="211" alt="image" src="https://github.com/user-attachments/assets/8cb974f6-b8a7-447e-b037-3dc410a926d0" />

<h2> OWASP Juice Shop </h2>

On the server, I set up the OWASP juice shop using docker.
I bound it directly to the servers LAN IP so Kali VM can reach it easily.

"docker run -d -p 192.168.1.8:3000:3000 --name juice-shop bkimminich/juice-shop"

I also made sure that this time the firewall was prepared and allowed traffic through port 3000.

On the Kali VM i curled the site and got a response so i opened it in the browser.
To save me the effort later i opened the browser through BurpSuite to be able to intercept at any point.

<img width="957" height="695" alt="image" src="https://github.com/user-attachments/assets/22ed4b83-a299-408f-b0f1-a54a9655751f" />

<h3> Attacks - OWASP Juice Shop </h3>

The first attack i attempted was a basic SQL injection with " ' OR 1=1-- " which succeeded.
I was then redirected to the Code Error detection page where i succesfully found the error and the fix to the problem.

<img width="929" height="815" alt="image" src="https://github.com/user-attachments/assets/00081aa9-bcd8-4827-8cc8-df1cca13d08f" />

<img width="905" height="581" alt="image" src="https://github.com/user-attachments/assets/2e7c8833-cd47-4e18-9509-dc3a3c697194" />

<h4> Finding the scoreboard page </h4>

The simple begining. An unprotected path to the page.

<img width="837" height="665" alt="image" src="https://github.com/user-attachments/assets/d52b381d-0574-45a0-a31e-3cb8071195a3" />

<h4> DOM XSS </h4>

Another simple attack. Just an XSS payload in the URL.

<img width="938" height="471" alt="image" src="https://github.com/user-attachments/assets/315ef1ab-9959-4a4d-8820-4d4d10b2d7fa" />

<h4> Web3 Sandbox </h4>

The Web3 sandbox for smart conracts also had an unprotected "/web3-sandbox" path.

<img width="940" height="591" alt="image" src="https://github.com/user-attachments/assets/b5a95664-db2b-443d-8b9a-9afffae945e5" />

<h2> Recon - MySQL </h2>

To attack the MySQL DB i nmapped my server.

<img width="770" height="294" alt="image" src="https://github.com/user-attachments/assets/2b3875aa-4263-4eb2-8fc8-1ab01fadb4fe" />

Nmap found the open 3306 port.

<h3> MySQL Brute-force </h3>

After the open port discovery Hydra found the weak credentials easily.

<img width="854" height="304" alt="image" src="https://github.com/user-attachments/assets/a1ff22f4-89ee-4877-8e41-0f23208daf7e" />

<h3> DB Manipulation </h3>

When the attacker gets the credentials he can easily get in.
The first login attempt threw a TLS/SSL error but that was bypassed with a simple "mysql -h 192.168.1.8 -u root -p --skip-ssl".
The attcker then inputs the found credentials and gains entry.

<img width="647" height="361" alt="image" src="https://github.com/user-attachments/assets/18c9ae8f-0f88-4c46-b953-1856836ac992" />

Here the the attacker can cause huge problems:

-Findings DBs

-Finding table names

-Finding data from the tables

<img width="455" height="141" alt="image" src="https://github.com/user-attachments/assets/4697e79c-b708-4def-b1ea-947665771488" />

-Creating backdoor users

<img width="592" height="92" alt="image" src="https://github.com/user-attachments/assets/14d5ea74-95a6-4997-9be6-5f6f0df13edc" />

<img width="671" height="211" alt="image" src="https://github.com/user-attachments/assets/23be0d4e-5972-46fa-aab7-c79c7a594160" />

-Insert false data

<img width="814" height="205" alt="image" src="https://github.com/user-attachments/assets/f29411d4-f47e-4b89-a74d-a41197f792aa" />

-Create and drop tables

<img width="515" height="214" alt="image" src="https://github.com/user-attachments/assets/aaa8665d-44d4-4664-9399-a7864bfa287d" />

<img width="332" height="44" alt="image" src="https://github.com/user-attachments/assets/220f848a-b398-47f5-9b23-0f588de90f29" />


Essentially the attacker gets completely unlimited access to the DB and its data.

<h3> Defensive measures - MySQL </h3>

First is setting up a proper password for the user that isnt guessable.
Something along the lines of:

"ALTER USER 'root'@'%' IDENTIFIED BY 'xK9#mP2$vL8nQ4wZ!rT6yU1c';"

Not wordlistable.

Then in order to avoid the Nmap scans i remade the DB.

docker network create --internal backend-net

docker run -d --name vuln-mysql \
  --network backend-net \
  -e MYSQL_ROOT_PASSWORD=xK9...randompassword \
  mysql:8.0

docker run -d --name webapp \
  --network backend-net \
  mywebapp

No -p flag on the DB at all now. The web app container will reach it by container name (vuln-mysql acts as a hostname on that network), but the attacker cant reach on port 3306 at all. This removed Hydra’s entire angle of attack. It can’t brute-force a port it can’t connect to.

Now there is no way to access it from outside so i will make a small web app container that can communicate with it.
First i decided to created a limited privlidge user, i just wanted to get this out the way before building the web app.

The limited privlidge user kinda functions as damage control. If the DB get SQL injected  it limits the amount of damage an attacker can do.

To connect to the shell as root i needed to use "docker exec -it vuln-mysql mysql -u root -p".
I recreated the structure. And created the user.

<img width="674" height="328" alt="image" src="https://github.com/user-attachments/assets/1bf9350e-3235-47ff-b439-7aa58d36ff73" />

After connecting as the new user i tested the privlidges.

<img width="745" height="418" alt="image" src="https://github.com/user-attachments/assets/e55abb04-10e5-4af0-bdf1-0a1ce6ef4b4e" />

Everything was properly protected.

<h1> Transfer app design </h1>

I decided the method for sending files was a bit long, having to remember the commands and type out file names.
To change that i created a custom app for file transfer between my server and any device connected to it.

The app was made in Visual Studio in C#.

First step was installing .NET SDK and the C# devkit.
The server is running OpenSSH and i decided to leave the transfer on a password basis so i could setup user accounts on it later.

For the file transfer i used SftpClient that runs over the same SSH connection already established.

On the server i made /incoming to drop all the test files into.

<img width="263" height="63" alt="image" src="https://github.com/user-attachments/assets/8d5bed28-a529-47f6-b783-89cfbde50491" />

To make a simple app the easiest way was using WinForms GUI.
All of the app files are in the Git repository.

Except for common issues there was nothing too complex that required debugging.

The first plan for a depoly was creating a deploy ".bat" file that actively clones into the Git repo and runs it.

That idea was ultimatly scrapped because to run on other devices they would have to have Git and .NET installed so its not really flexible.

And i just had no idea that creating a ".exe" file was as simple as it is. So i made the "SshSenderApp.exe" that ran it properLy. 

<img width="581" height="503" alt="image" src="https://github.com/user-attachments/assets/c6ce4fbd-e2f7-41d2-9441-ccba94bea0f6" />

<img width="1424" height="733" alt="image" src="https://github.com/user-attachments/assets/4314a90c-6a69-41eb-9652-1d7610557aaf" />


<h2> Mobile adaptation </h2>

I really wanted to run the app over any android device, and eventually IOS, so i used Avalonia Framework to make the enitre app cross-platform ready.

Using the developer mode on my phone and adb pair/adb connect commands i managed to setup wireless debugging on my phone.
This could have been avoided and made easier if i just had a data transfer cable on hand but i didnt.

The app itself looks identical on the Android device. I tested it by using a device in a completely different city that was logged into my Tailnet.

To make the ".apk" file i used the command "dotnet publish SshSenderApp.Android -c Release -p:AndroidPackageFormat=apk"

I considered uploading to the PlayStore people i want to install it can access it more easily but that is a paid service so i decided not to.

Delivering the ".apk" file to other users is a bit annoying as i need to use a large file transfer service because the Tailscale service refuses contact with Android devices, this problem i still havent uncovered as it works with IOS devices and any PC i use, not to mention it succesfully pings the the Android devices but still refuses to send files.

But when the ".exe" is delivered and ran it works perfectly fine!

Data can be uploaded, sent and dowloaded across platforms.

<h2> Remote updates </h2>

Every time i changed something in the app i had to rebuild it and resend the ".exe" and ".apk" to every device.
That got annoying fast so i decided the app should update itself from my server.

On the server i installed nginx and made it serve a single folder, "/srv/updates", on port 8080.
That folder only holds 3 files: the ".exe", the ".apk" and "version.json".

The "version.json" file holds the newest version number and a SHA-256 checksum for both files.
When the app starts it reads "http://<server>:8080/version.json" and compares that version with its own.
If the server has a newer one an "Update available" box shows up on the login screen.

On Windows the app downloads the new ".exe", verifies the checksum, swaps itself with the new file and restarts.
On Android it downloads the ".apk" and opens the system installer. I still have to tap "Install" every time because Android doesnt allow silent installs outside of the PlayStore.

To make releasing easier i rewrote "publish.bat".
It asks for the new version number, builds both the ".exe" and the ".apk", writes "version.json" with the checksums and uploads everything to the server with scp.

The first time i ran it i thought the script froze. It was just waiting for me to press Enter on the version number.

<h3> Problems - Remote updates </h3>

The first install of the new version still had to be done by hand, since the old app didnt have the updater in it yet.
On my phone the ".apk" refused to open with "no app to open this with". The file had been downloaded twice and got a "(1)" in its name. After renaming it back to "SshSender.apk" it installed normally.

Then the update check failed with "No route to host".
nginx was running and "curl -i http://localhost:8080/version.json" on the server returned the file, but neither my PC nor my phone could reach it.

The cause was firewalld again, the same firewall that locked me out of Cockpit.
I first checked ufw which was inactive, so it took a while to realize that firewalld was the one dropping the port.

I opened port 8080 only for my home network and my Tailnet:

sudo firewall-cmd --permanent --add-rich-rule='rule family="ipv4" source address="192.168.1.0/24" port port="8080" protocol="tcp" accept'

sudo firewall-cmd --permanent --add-rich-rule='rule family="ipv4" source address="100.64.0.0/10" port port="8080" protocol="tcp" accept'

sudo firewall-cmd --reload

After that the check went through and reported that i already had the newest version.
To test the whole thing i published version 1.0.2, the phone picked it up, downloaded it and installed it.

<h3> Security - Remote updates </h3>

The firewall rules only allow the update port from my LAN and my Tailnet.

The updates themselves travel over plain HTTP. Over Tailscale that traffic is encrypted anyway and the checksum catches a damaged download.
Android also refuses an ".apk" that is signed with a different key than the installed app.
The Windows ".exe" doesnt have that protection, so HTTPS or a signed update is something to add later.

The ".apk" is currently signed with the debug key from my PC. If that key gets lost the phones will refuse updates until the app is reinstalled, so making a proper keystore is on the list.

<h2> App redesign </h2>

The Avalonia app worked but looked pretty plain, so i gave it a new look.

Everything now sits in rounded cards, the colors follow the light or dark mode of the device, folders and files got their own icons and the main buttons like "Connect" and "Upload" are highlighted.

The layout is made phone first. On a phone it fills the screen, while on a tablet or PC it stays a centered column instead of stretching across the whole window.
The progress bar and status text only show up when there is something to show, which leaves more room for the file list.

The whole redesign is a single file, "MainView.axaml", so it can go out to all devices as a normal update through the update server.

<h1> User accounts </h1>

If i plan to spread the app and server access to friends and family it is a good idea to add specific user accounts on the server.

This is fairly simple with "sudo adduser <username>".

<img width="586" height="60" alt="image" src="https://github.com/user-attachments/assets/868d43e6-d307-4f33-a456-d51ab7ee9e79" />

Of course privlidges have to be managed otherwise all users have root access on the server. Not that i would allow anyone i dont trust to use my server but if others use it, being a bit less tech savy, a problem could arise so its best to prevent it altogether.

<h1> Wireshark traffic analysis </h1>

Analysis covering a file upload to the server.

<img width="1124" height="62" alt="image" src="https://github.com/user-attachments/assets/d4496ce8-b24c-4e1e-b98e-c097fd678161" />

The traffic was isolated with the display filter "tcp.port == 65223". In the picture the first 4 frames caught the TCP handshake between the server and PC.  
The SYN --> SYN, ACK --> ACK pretty much bolis down to 

Server: "Can we talk?"
Client: "Yes"
Server: 'handshake done'

<img width="1011" height="34" alt="image" src="https://github.com/user-attachments/assets/de416414-81e4-4949-8344-99cb8abd881a" />

These are just version banners. Here we can see the Renci.SshNet, in other words the .NET library that my C# app uses.

<img width="1190" height="208" alt="image" src="https://github.com/user-attachments/assets/60d28f69-75cf-4ced-a231-a46c64b54bd0" />

This is the actual key exchange. Initialized by the server in line 18. This is the only phase that isnt encrypted so Wireshark can label it.

<img width="989" height="494" alt="image" src="https://github.com/user-attachments/assets/be0b18ca-bef5-4a19-a406-113bf48e9647" />

<img width="1188" height="208" alt="image" src="https://github.com/user-attachments/assets/f3196bdc-9467-4105-a3bb-aff7db4717ff" />

This is the first of two encrypted bursts of data. This one is the authentication and opening of the transfer channel.

<img width="1194" height="417" alt="image" src="https://github.com/user-attachments/assets/f1b5cc41-b418-4eb4-8815-916cad7857eb" />

And this is the second burst which is the actual file transmission.

<img width="974" height="117" alt="image" src="https://github.com/user-attachments/assets/a23eee44-55a0-4ae0-b452-96df204f505d" />

This is the final disconnect. We see the FIN from both sides.

Some packets had the "[TCP SPURIOUS RETRANSMISSION]" flag. This is most likely just a harmless capture artefact. The same packet got recorded twice or in a slightly different order.

<h1> Database setup </h1>

I set up a basic MySQL DB on the server using docker.

<img width="669" height="386" alt="image" src="https://github.com/user-attachments/assets/261d43b6-832d-4f60-be19-bd2720f81704" />

I ran the command to make the DB and just made a basic table with random data inside.

<img width="712" height="499" alt="image" src="https://github.com/user-attachments/assets/ec637ed0-b2c5-41c0-a9bb-2ac67e568807" />
