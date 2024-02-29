# March 2024: Ownership change
***
EN: Starting from March 1st, 2024, [ROMAWO GmbH](https://romawo.com) will assume all rights and obligations of the package manager RuckZuck.tools. RuckZuck will continue to be freely available thanks to [sponsors](https://ruckzuck.tools/Home/Sponsors). The client tools will remain an open-source solution managed on GitHub. The server components will be removed from the GitHub repository and will no longer be publicly accessible.
ROMAWO will utilize the RuckZuck repository for commercial purposes and will update and maintain products associated with this use in the [public repository](https://ruckzuck.tools/Home/Repository). Products that have not been published in the repository in ROMAWO's interest must continue to be updated by the community. Unused or outdated products will be removed from the repository.
ROMAWO assumes no liability and is not responsible for the licensing of the software in the public repository.
***
DE: Ab dem 1. März 2024 übernimmt die [ROMAWO GmbH](https://romawo.com) sämtliche Rechte und Pflichten des Paket-Managers RuckZuck.tools. RuckZuck wird weiterhin dank der [Sponsoren](https://ruckzuck.tools/Home/Sponsors) kostenlos nutzbar sein. Die Client-Tools bleiben eine Open-Source-Lösung, welche auf GitHub verwaltet werden. Die Serverkomponenten werden aus dem GitHub-Repository entfernt und sind nicht mehr öffentlich einsehbar.
ROMAWO wird das RuckZuck-Repository für kommerzielle Zwecke nutzen und die mit dieser Nutzung verbundenen Produkte auch im [öffentlichen Repository](https://ruckzuck.tools/Home/Repository) aktualisieren und pflegen. Produkte, die nicht im Interesse von ROMAWO im Repository veröffentlicht wurden, müssen wie bisher von der Community aktualisiert werden. Ungenutzte oder veraltete Produkte werden aus dem Repository entfernt.
ROMAWO übernimmt keine Haftung und ist nicht verantwortlich für die Lizenzierung der Software im öffentlichen Repository.

***
Software Package Manager for Windows provides a quick way to install and update software....

<img src="https://cloud.githubusercontent.com/assets/11909453/24813479/7340c22a-1bce-11e7-8df7-a0d8236775df.png" width="520">


Select a software from the repository and RuckZuck handles the download and installation for you.
 RuckZuck can detect and update existing software that was not installed with RuckZuck. 

 * Main Page: https://RuckZuck.tools
 * Software Repository: https://ruckzuck.tools/Home/Repository
 * RSS Feed: http://ruckzuck.tools/rss.aspx

 The RuckZuck repository does not store binaries of the software, just links to where the software is downloaded. Installing software with RuckZuck does not grant you a license for that product.



# Aug22: ApiKey will be required to get definitions
As the Api is more and more overloaded with requests that will bulk dump the software definitions, the API function "GetSoftwares" will require an APIKey. API-Keys can be requested by sending me a DM or by opening an Issue here on GitHub.

# Changes in V1.7.x:

New REST API which brings some general changes:
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

2019: https://rzander.azurewebsites.net/ruckzuck-figures-for-2019/

2020: https://rzander.azurewebsites.net/ruckzuck-figures-for-2020/

2021: https://rzander.azurewebsites.net/ruckzuck-figures-2021/

2022: https://rzander.azurewebsites.net/ruckzuck-figures-2022/

## RuckZuck components
### RuckZuck_Tool (RuckZuck.exe)
The RuckZuck.exe is a portable tool with a UI to install or update applications from the RZ repository.

### RZGet (RZGet.exe)
Successor of `RZUpdate.exe`.
```
Install:
Install a Software from Shortname : RZGet.exe install "<Shortname>"[;"<Shortname2>"] [/cleanup]
Install a Software from JSON File : RZGet.exe install "<JSON full path>"[;"<JSON full path>"]
Install a Sepcific Version : RZGet.exe install --name "<ProductName>" --vendor "<Manufacturer>" --version "<ProductVersion>"

Update:
Update all missing updates : RZGet.exe update --all [--retry] [--user]
Update all missing updates : RZGet.exe update --all --exclude "<Shortname>"[;"<Shortname2>"] [--retry] [--user]
Show all missing updates (delay=days after release) : RZGet.exe update --list --all [--user] [--allusers] [--delay=5]
check if a Software requires an update : RZGet.exe update --list "<Shortname>" [--user]
Update a Software from Shortname : RZGet.exe update "<Shortname>"[;"<Shortname2>"] [--retry] [--user]

Show:
Show Metadata : RZGet.exe show "<Shortname>"
Show Metadata for a specific Version : RZGet.exe show --name "<ProductName>" --vendor "<Manufacturer>" --version "<ProductVersion>"

Search:
Show full Catalog JSON: RZGet.exe search
Search for a Keyword: RZGet.exe search zip
Search SW in a Category: RZGet.exe search --categories compression
Search for installed SW: RZGet.exe search --isinstalled true
Search for a manufacturer: RZGet.exe search --manufacturer zander
Search for a shortname and return PowerShell Object: RZGet.exe search --shortname ruckzuck | convertfrom-json

UnInstall:
UnInstall a Software from Shortname : RZGet.exe uninstall "<Shortname>"[;"<Shortname2>"] [/cleanup]
UnInstall a Software from JSON File : RZGet.exe uninstall "<JSON full path>"[;"<JSON full path>"]
UnInstall a Sepcific Version : RZGet.exe uninstall --name "<ProductName>" --vendor "<Manufacturer>" --version "<ProductVersion>"
```

### OneGet Provider (depreciated)
A Provider for [OneGet](https://github.com/OneGet/oneget) (part of Win10) to update or install applications from the RZ repository with powerShell.

> Examples on: https://github.com/rzander/ruckzuck/wiki/RuckZuck-OneGet-Provider

### RZ for Configuration Manager
Allows applications to be imported from the RZ repository into Microsofts System Center Configuration Manager from a UI. It will create ConfigMgr Applications, DeploymentType, Collection and a Deployment. V1.5.1.8 news: https://rzander.azurewebsites.net/ruckzuck-for-configmgr-new-v1-5-18/


