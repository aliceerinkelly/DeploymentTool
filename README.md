# Windows Deployment & Driver Management Tool

A streamlined, user-friendly utility designed to backup working system drivers, strip unwanted Windows editions from a `.wim` file, slipstream your drivers, and output a lightweight, custom installation image.

## 🚀 Quick Start Guide

Follow these steps to create your own customized Windows 11 installation media:

1. **Download the Windows ISO**  
   Grab a fresh Windows 11 `.iso` directly from [Microsoft's Official Software Download Page](https://microsoft.com).

2. **Extract the Image**  
   Locate and extract the `install.wim` file from the `/sources/` directory inside the downloaded ISO, and save it to your desktop.

3. **Set Up Your Workspace**  
   Create a new folder anywhere on your computer (desktop is recommended) to act as your working directory.

4. **Prepare the Files**  
   Paste both `DeploymentTool.exe` and your extracted `install.wim` into this new folder.

5. **Launch the Tool**  
   Right-click `DeploymentTool.exe` and select **Run as Administrator**. Agree to the end-user terms to proceed.

6. **Backup Existing Drivers**  
   Click the **Backup Drivers** button. A new folder populated with your active, working system drivers will instantly appear in your workspace. 
   *(Note: If you only intended to back up your current drivers, you can safely close the tool and stop here!)*

7. **Scan & Open the Image**  
   Click **Scan and Slipstream Image**. The utility will automatically detect your `install.wim` file and load its contents.

8. **Manage Windows Editions**  
   Review the detected Windows editions displayed in the green area at the bottom of the interface. 
   * Use the interface to **delete the editions you do not want** to save disk space.
   * *Important:* You will need to inject drivers into each individual edition that you decide to keep.

9. **Slipstream Your Drivers**  
   Select your remaining Windows editions one at a time to slipstream your backed-up drivers directly into them.

10. **Finalize and Package Your Media**  
    Once processing is complete, you have two options to rebuild your installer:
    * **Option A (Standard WIM):** Place your modified `install.wim` back into the ISO's `/sources/` directory, then use a tool like **Rufus** to burn the ISO to a bootable USB thumb drive.
    * **Option B (Compressed ESD):** Use the tool to compress the heavy `.wim` into a significantly smaller `install.esd` file. Delete the original `install.wim` from the ISO's `/sources/` folder, drop your new `install.esd` in its place, and burn it to a DVD or flash drive using Rufus.

💡 **Pro-Tip (The Autounattend Button):**  
Clicking the `autounattended.xml` button will temporarily disable Windows AV and Firewall restrictions during deployment. This automates the setup sequence, reducing the installation process down to just **two quick questions**: *What drive?* and *What network?* 

Ta-da! You are completely finished. Enjoy your custom, streamlined Windows installation!
