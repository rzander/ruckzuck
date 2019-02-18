<img src="https://github.com/rzander/ruckzuck/blob/master/RZ.Server/RZ.Server/wwwroot/images/RZ-Logo.png">
Software Package Manager for Windows provides a quick way to install and update software....

<img src="https://cloud.githubusercontent.com/assets/11909453/24813479/7340c22a-1bce-11e7-8df7-a0d8236775df.png" width="520">


Select a software from the repository and RuckZuck handles the download and installation for you.
 RuckZuck can detect and update existing software that was not installed with RuckZuck. 

 * Main Page: https://RuckZuck.tools
 * Software Repository: http://ruckzuck.tools/Repository.aspx
 * RSS Feed: http://ruckzuck.tools/rss.aspx

 The RuckZuck repository does not store binaries of the software, just links to where the software is downloaded. Installing software with RuckZuck does not grant you a license for that product.

# Changes in V.Next (Soon):

RuckZuck will soon get a new REST API which brings some general changes:
* No RuckZuck accounts and therefore no authentication required anymore. 
  * You will be able to provide an E-Mail address if you upload new software, but as soon as the software is approved, the address will be removed from the package.
  * Moderators will have to log in with a Microsoft account.
  * No benefits for existing 'PRO' users (accounts will be deleted).
* It is possible that RuckZuck will store binaries for some packages.
  * If a product does not provide a URL for automatic download and the license allows redistribution of binaries, RuckZuck will be able to host these files.
* Support for private/disconnected repostories if you host your own RuckZuck Server (no sync from the public repository).

## Statistics and Figures of the Project: 

2015:  http://rzander.azurewebsites.net/ruckzuck-packagemanager-v1-0/ 

2016:  https://rzander.azurewebsites.net/ruckzuck-figures-for-2016/ 

2017:  https://rzander.azurewebsites.net/ruckzuck-figures-for-2017/

2018:  https://rzander.azurewebsites.net/ruckzuck-figures-for-2018/

## RuckZuck components
### RuckZuck_Tool (RuckZuck.exe)
The RuckZuck.exe is a portable tool with a UI to install or update applications from the RZ repository.

### RZUpdate (RZUpdate.exe)
RZUpdate.exe is a portable command-line tool to install or update applications from the RZ repository.

### OneGet Provider
A Provider for [OneGet](https://github.com/OneGet/oneget) (part of Win10) to update or install applications from the RZ repository with powerShell.

> Examples on: https://github.com/rzander/ruckzuck/wiki/RuckZuck-OneGet-Provider

### RZ for Configuration Manager
Allows applications to be imported from the RZ repository into Microsofts System Center Configuration Manager from a UI. It will create ConfigMgr Applications, DeploymentType, Collection and a Deployment. V1.5.1.8 news: https://rzander.azurewebsites.net/ruckzuck-for-configmgr-new-v1-5-18/

### RuckZuck Caching Service ###
Docker container to cache RuckZuck traffic (SW definitions, Icons and binary-downloads).
https://rzander.azurewebsites.net/ruckzuck-cache/   
https://hub.docker.com/r/zanderr/ruckzuck/

### RZ.Server
RuckZuck Web-UI and REST API.

### RZ.Bot
(internals) A "Service" to automatically download and install failed apps.
### RZ.LogConsole
(internals) A real-time Log-Console to get failed and success installations.

