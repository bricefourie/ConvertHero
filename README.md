# ConvertHero.Core

Forked from [Dirmigurt/ConverHero](https://github.com/Dirtmigurt/ConvertHero)

ConvertHero.Core is a library to convert a midi file .mid to .chart Clone Hero.
ConvertHero.Core is able to create 5 Frets Clone Hero chart for Lead Guitar only.

ConvertHero.Core can merge multiple tracks from Midi file, but be sure to edit your midi file to get only the track you want to convert

ConvertHero.Core is a library made in .Net Standard 2.1

## Usage

```c#
using ConvertHero.Core.Services;
...
Stream midiStream = new FileStream("Path of your .mid file",FileMode.Open);
IChartService service = new ChartService(midiStream);
Stream chartStream = service.ConvertToChart("Name of the song");
...
```
