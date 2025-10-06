## Sparking Zero File Extractor GUI
This is the tool attached on the Sparking Zero Audio Modding Tool releases ([Mod Page](https://gamebanana.com/tools/18312), [Usage](https://docs.google.com/document/d/1hjCoHq5XxsIRARTcqUn12roO_SVsuiYhDwmwWXCrDQ0/edit?tab=t.5bdxkeqf18e5#heading=h.tyg11g670f9i))

The SZ File Extractor is an alternative to Fmodel for extracting audio and locres files in particular
from Sparking Zero, with the ability to filter and know the respective name of the character or type of
file rather than relying on online lists. All languages supported for character names.

Works with any Sparking Zero version, and fixes the corrupted files issue Fmodel has as far as I've been told.

https://github.com/user-attachments/assets/cf5993fe-aaac-45de-8538-865205389bac



Please view the usage guide linked earlier for details

This requires my [SZ Extractor Server](https://github.com/LostImbecile/SZ_Extractor_Server/releases), which is included in releases.

### Note to devs
- `Services/FileInfo/ContentTypeService.cs` contains the filters and menu item tabs, if you want to add more paths or sections it's easy to do it from there
- The filter uses regex, if you need more controls or optimisations/caching you can modify the extractor server

This was rushed during writing due to time constraints so if you have any questions feel free to contact me, comments are sparse
but there's hopefully little to modify for changes you may need.

For info on LocresLib see [here](https://github.com/akintos/UnrealLocres), I use it to read locres files and match their
names with the ID of the character for all languages.
