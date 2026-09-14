1. download a new windows .iso from  microsoft ( https://www.microsoft.com/en-us/software-download/windows11  )
2. extract the install.wim from the iso /sources/ directory to your desktop
3. make a folder with any name .  i prefer to work on my desktop
4. paste your DeploymentTool.exe  and install.wim in the same folder
5.run DeploymentTool.exe and agree not to sue me. also it should b run as administrator
6. run the "backup drivers".  a new folder full of your working drivers will appear in your folder
7. at this point if you only want to backup your drivers you can stop
8. click scan and slipstream image.  it will detect your .wim file and open it
9. in the green area below it will show the edditions inside that wim file.
10.you can delete the edditions you dont want. you will have to put drivers in all the ones you keep
11. when your happy with how many you have you can choose them one at a time to slipstream drivers into them
12. when all thats done you can either put the wim file back in the iso files/sources/ directory
and rufus the iso to a thumbdrive or
13.  compress the wim to a smaller install.esd file.  in this case delete the wim in the iso and drop in the /sources/ directory
14. rufus it or write to dvd

if you want the autounattended.xml click that button.  it "should" temporarily disable windows av and firewall as well as
make the install only 2 questions.  what drive and what network

tada! your done you made a custom iso of winblows 11 (mayve win 10 too) that includes all your drivers and lets you play
