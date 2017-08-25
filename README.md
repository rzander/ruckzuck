# RuckZuck
RuckZuck is a Software Package Manager for Windows, a quick way to install and update Software....

<img src="https://cloud.githubusercontent.com/assets/11909453/24813479/7340c22a-1bce-11e7-8df7-a0d8236775df.png" width="520">


Select a Software from the Repository and RuckZuck handles the download and the Installation for you.
 RuckZuck is able to detect and update existing Software that was not installed with RuckZuck. 

 * Main Page: https://RuckZuck.tools
 * Software Repository: http://ruckzuck.tools/Repository.aspx
 * RSS Feed: http://ruckzuck.tools/rss.aspx

 The RuckZuck repository does not store the binaries of the Software, just the links to where the software is downloaded. Installing Software with RuckZuck does not grant you a license for that Product.

## Statistics and Figures of the Project: 

2015:  http://rzander.azurewebsites.net/ruckzuck-packagemanager-v1-0/ 

2016:  https://rzander.azurewebsites.net/ruckzuck-figures-for-2016/ 

## RuckZuck components
### RuckZuck_Tool (RuckZuck.exe)
The RuckZuck.exe is a portable Tool with a UI to install or update Applications from the RZ Repository.

### RZUpdate (RZUpdate.exe)
RZUpdate.exe is a portable command-line tool to install or update Applications from the RZ Repository.

### OneGet Provider
A Provider for [OneGet](https://github.com/OneGet/oneget) (part of Win10) to update or install Applications from the RZ Repository with PowerShell.

### RZ for Configuration Manager
allows to import Applications from the RZ Repository into Microsofts System Center Configuration Manager from a UI. It will create ConfigMgr Applications, DeploymentType, Collection and a Deployment. V1.5.1.8 news: https://rzander.azurewebsites.net/ruckzuck-for-configmgr-new-v1-5-18/

### RuckZuck Caching Service ###
Docker container to cache RuckZuck traffic (SW definitions, Icons and binary-downloads).
https://hub.docker.com/r/zanderr/ruckzuck/

### RZ.Bot
(internals) A "Service" to automatically download and install failed apps.
### RZ.LogConsole
(internals) A real-time Log-Console to get failed and success installations.
### RZ.WCF
(internals) RuckZuck Back-End Web-Service. [Overview](https://rzander.azurewebsites.net/ruckzuck-backend-services-v2/)
