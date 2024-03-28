using System;
using System.Collections.Generic;
using System.Text;

namespace ConvertHero.Core
{
    public static class Resources
    {
        public static readonly string EventsTrack = "[Events]\r\n{{\r\n{0}\r\n}}";
        public static readonly string NoteTrack = "[{0}]\r\n{{\r\n{1}\r\n}}";
        public static readonly string SongSection = "[Song]\r\n{{\r\n  Name = \"{1}\"\r\n  Artist = \"\"\r\n  Charter = \"\"\r\n  Offset = 0\r\n  Resolution = {0}\r\n  Player2 = bass\r\n  Difficulty = 0\r\n  PreviewStart = 0\r\n  PreviewEnd = 0\r\n  Genre = \"\"\r\n  MediaType = \"\"\r\n  MusicStream = \"song.ogg\"\r\n}}";
        public static readonly string SyncTrack = "[SyncTrack]\r\n{{\r\n{0}\r\n}}";
    }
}
