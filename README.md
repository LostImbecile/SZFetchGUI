## Sparking Zero File Extractor GUI
This is the tool attached on the Sparking Zero Audio Modding Tool releases ([Mod Page](https://gamebanana.com/tools/18312), [Usage](https://docs.google.com/document/d/1hjCoHq5XxsIRARTcqUn12roO_SVsuiYhDwmwWXCrDQ0/edit?tab=t.5bdxkeqf18e5#heading=h.tyg11g670f9i))

![Example Screenshot](https://i.imgur.com/JLVT0Ei.png)

Please view the usage guide linked earlier for details

This requires my [SZ Extractor Server](https://github.com/LostImbecile/SZ_Extractor_Server/releases).

### Note to devs
- `Services/FileInfo/ContentTypeService.cs` contains the filters and menu item tabs, if you want to add more paths or sections it's easy to do it from there
- The filter uses regex, if you need more controls or optimisations/caching you can modify the extractor server

This was rushed during writing due to time constraints so if you have any questions feel free to contact me
