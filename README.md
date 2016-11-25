SupRip
======

Original source received from developer (exar.ch/suprip). Original app is not under development anymore since 2 years.

Depends on MODI, a Microsoft ActiveX control.
To install that see http://support.microsoft.com/kb/982760

Download free Sharepoint designer installer there and select only "Microsoft Office Document Imaging". 
Make sure you select also all the children of that option, otherwise you get a "Object hasn’t been initialized and can’t be used yet" 
error.

This project has been tested on Visual Studio 2010 and 2015, and it compiles and works ok.

If you select "Release" as build mode and run it, it won't hang on the errors it gives about OCR in the debug mode.

The relevant MDI files are installed (on x64) under:
  C:\Program Files (x86)\Common Files\Microsoft Shared\MODI\12.0
