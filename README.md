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


<h1> Attacks </h1>

Here i will go over any current and future attacks that i attempt on the server.
All attacks will be directed from a kali VM on my local PC.
The point of the attacks is to serve as an actual test of the setup, whether they succeed or not if they are properly executed the test is valid and so are the results.

<h2> Recon </h2>

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

<h2> SSH Brute-force </h2>

I used the hydra tool on the Kali Vm to brute-force the server with rockyou.txt

The exact attack line was:   hydra -l smustac -P /usr/share/wordlists/rockyou.txt -t 4 ssh://192.168.1.8

Setting the username as "smustac", specifying the .txt file we want to pull from and limiting to 4 parallel connections with "-t 4".

<img width="1905" height="979" alt="image" src="https://github.com/user-attachments/assets/8de43c6f-36e8-47df-9735-ea74917862c9" />

A properly set password proved to be too complex for this attack, which is exactly what was desired. 

<h3> Defensive measure </h3>

Even though the attack was unsuccessfull its still not desired for these types of attacks to be allowed on the server.

So i decided to install fail2ban but this once again shutdown my SSH connection to the server.
This could have happened either because there was a failed login attempt from my PC at some point which caused fail2ban to ban it or Kali at some point used a the same IP as my PC while attacking, which was then later given to my PC and the fail2ban ban applied to it.

The solution to ensure testing integritiy is to use DHCP reservation on Kali so it only ever gets the selected IP and doesnt cause these "bleed-over" bans.

First i couldnt figure out why my bridged adapter wasnt taking on the reserved IP but it turns out i just had it set to the wrong adapter on my actual PC.

After that it worked exactly as wanted.

<img width="497" height="66" alt="image" src="https://github.com/user-attachments/assets/33be4243-6b43-4939-b5eb-66ded7d6facd" />

To test i once again began the brute-force attack and the IP assigned to the Kali VM was succeesfully banned.

<img width="534" height="211" alt="image" src="https://github.com/user-attachments/assets/8cb974f6-b8a7-447e-b037-3dc410a926d0" />



