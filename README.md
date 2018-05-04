# ToriKyttä

ToriKyttä, or ToriKyttae, is a command line driven application for retrieving classifieds from the number one marketplace in Finland for all things related to music production, the Tori-section (="*Marketplace*") of Muusikoiden.net. ToriKyttä is written in C# and targets .NET Core 2.0.

The (one man) development team of ToriKyttä is by no means affiliated with Muusikoiden.net or Muusikoiden Net ry, the registered association behind the website.

## Goal

The goal is to have a Windows- and Linux-friendly executable that can query the Tori-search as well and easy as it can be queried from the actual search form found from Tori search page, and to be able to output the search results in both machine- and human-readable format, which probably is XML.

For developers, there should be a nice class library that would contain classes for query request (and all it's parameters), response and classified.

## Possible applications

I can't really figure out any other applications for ToriKyttä than 1) to develop a search watch with it or 2) to search and browse Tori without a graphical interface.

## Legal and ethical things to consider

For starters, I or anyone else who have contributed to this project is NOT BY ANY MEANS responsible of anything. If you cause damage or butt pain in any way anywhere, in this or any other realm of existence, you are responsible for it.

While in 4th of May 2018 the [Terms of Use](https://muusikoiden.net/extra/disclaimer.php) of Muusikoiden.net do not forbid automating the use of their website or using web scrapers (which ToriKyttä certainly is), one should understand that using a web scraper, rapid-firing HTTP requests through paginated search results, is something the people behind a API-less Muusikoiden.net didn't take into question during developing the website. They do have a notion there about reserving the right to modify the Terms of Use anytime they want. So before you start using ToriKyttä, make sure you've read their Terms of Use.

So if you're going to automate ToriKyttä by firing up multiple searches at once or skimming through search results with multiple pages, be graceful and kind; don't rapid-fire it like a madman. Muusikoiden Net -association is a non-profit organization and their website has done a lot for Finnish musicians and musical enthusiasts.