using System;
using System.Collections.Generic;
using System.Globalization;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Reads a geometry from its Well-Known Text (WKT) representation. Accepts all seven subtypes,
    /// nested collections, and <c>EMPTY</c>; tolerant of surrounding whitespace and of both the
    /// canonical parenthesized MULTIPOINT form and the legacy bare-coordinate form. An ISO dimensional
    /// tag (<c>Z</c> / <c>M</c> / <c>ZM</c>) is honored; when absent, a coordinate's extra ordinates are
    /// auto-detected (a 3rd ordinate is Z, a 4th is M). An optional PostGIS <b>EWKT</b>
    /// <c>SRID=&lt;n&gt;;</c> prefix is accepted — the embedded SRID overrides the argument passed to
    /// <see cref="Read"/>. Numbers are parsed in the invariant culture. Malformed input raises
    /// <see cref="GeoFormatException"/>.
    /// </summary>
    public sealed class GeoWktReader
    {
        /// <summary>Reads a geometry from WKT, assigning the specified SRID unless an EWKT prefix embeds one.</summary>
        /// <param name="wkt">The WKT (or EWKT) string.</param>
        /// <param name="srid">The SRID to assign when the text does not embed one.</param>
        public GeoGeometry Read(string wkt, int srid = GeoGeometry.DefaultSrid)
        {
            if (wkt == null)
                throw new ArgumentNullException(nameof(wkt));
            var parser = new Parser(wkt, srid);
            parser.ConsumeSridPrefix();
            GeoGeometry geometry = parser.ParseGeometry();
            parser.EnsureAtEnd();
            return geometry;
        }

        private sealed class Parser
        {
            private readonly string mText;
            private int mSrid;
            private int mPos;

            public Parser(string text, int srid)
            {
                mText = text;
                mSrid = srid;
                mPos = 0;
            }

            public void ConsumeSridPrefix()
            {
                SkipWhitespace();
                int save = mPos;
                if (mPos >= mText.Length || !IsLetter(mText[mPos]))
                    return;
                if (ReadWord() != "SRID")
                {
                    mPos = save;
                    return;
                }
                SkipWhitespace();
                if (mPos >= mText.Length || mText[mPos] != '=')
                    throw Error("'=' expected after SRID");
                mPos++;
                int srid = ReadSridValue();
                SkipWhitespace();
                if (mPos >= mText.Length || mText[mPos] != ';')
                    throw Error("';' expected after the SRID value");
                mPos++;
                mSrid = srid;
            }

            public GeoGeometry ParseGeometry()
            {
                string keyword = ReadWord();
                ReadDimensionTag(out bool tagged, out bool hasZ, out bool hasM);
                switch (keyword)
                {
                    case "POINT": return ParsePoint(tagged, hasZ, hasM);
                    case "LINESTRING": return ParseLineString(tagged, hasZ, hasM);
                    case "POLYGON": return ParsePolygon(tagged, hasZ, hasM);
                    case "MULTIPOINT": return ParseMultiPoint(tagged, hasZ, hasM);
                    case "MULTILINESTRING": return ParseMultiLineString(tagged, hasZ, hasM);
                    case "MULTIPOLYGON": return ParseMultiPolygon(tagged, hasZ, hasM);
                    case "GEOMETRYCOLLECTION": return ParseGeometryCollection();
                    default: throw Error($"unknown geometry keyword '{keyword}'");
                }
            }

            private GeoGeometry ParsePoint(bool tagged, bool hasZ, bool hasM)
            {
                if (ConsumeEmpty())
                    return GeoPoint.Empty(mSrid);
                Expect('(');
                GeoCoordinate coordinate = ReadCoordinate(tagged, hasZ, hasM);
                Expect(')');
                return new GeoPoint(coordinate, mSrid);
            }

            private GeoGeometry ParseLineString(bool tagged, bool hasZ, bool hasM)
            {
                if (ConsumeEmpty())
                    return new GeoLineString(new List<GeoCoordinate>(), mSrid);
                return new GeoLineString(ReadCoordinateList(tagged, hasZ, hasM), mSrid);
            }

            private GeoGeometry ParsePolygon(bool tagged, bool hasZ, bool hasM)
            {
                if (ConsumeEmpty())
                    return new GeoPolygon(new List<List<GeoCoordinate>>(), mSrid);
                return new GeoPolygon(ReadRingList(tagged, hasZ, hasM), mSrid);
            }

            private GeoGeometry ParseMultiPoint(bool tagged, bool hasZ, bool hasM)
            {
                var points = new List<GeoPoint>();
                if (ConsumeEmpty())
                    return new GeoMultiPoint(points, mSrid);
                Expect('(');
                do
                {
                    points.Add(ReadMultiPointMember(tagged, hasZ, hasM));
                }
                while (ConsumeComma());
                Expect(')');
                return new GeoMultiPoint(points, mSrid);
            }

            private GeoPoint ReadMultiPointMember(bool tagged, bool hasZ, bool hasM)
            {
                if (ConsumeEmpty())
                    return GeoPoint.Empty(mSrid);
                if (PeekNonWhitespace() == '(')
                {
                    Expect('(');
                    GeoCoordinate coordinate = ReadCoordinate(tagged, hasZ, hasM);
                    Expect(')');
                    return new GeoPoint(coordinate, mSrid);
                }
                return new GeoPoint(ReadCoordinate(tagged, hasZ, hasM), mSrid);
            }

            private GeoGeometry ParseMultiLineString(bool tagged, bool hasZ, bool hasM)
            {
                var lines = new List<GeoLineString>();
                if (ConsumeEmpty())
                    return new GeoMultiLineString(lines, mSrid);
                Expect('(');
                do
                {
                    if (ConsumeEmpty())
                        lines.Add(new GeoLineString(new List<GeoCoordinate>(), mSrid));
                    else
                        lines.Add(new GeoLineString(ReadCoordinateList(tagged, hasZ, hasM), mSrid));
                }
                while (ConsumeComma());
                Expect(')');
                return new GeoMultiLineString(lines, mSrid);
            }

            private GeoGeometry ParseMultiPolygon(bool tagged, bool hasZ, bool hasM)
            {
                var polygons = new List<GeoPolygon>();
                if (ConsumeEmpty())
                    return new GeoMultiPolygon(polygons, mSrid);
                Expect('(');
                do
                {
                    if (ConsumeEmpty())
                        polygons.Add(new GeoPolygon(new List<List<GeoCoordinate>>(), mSrid));
                    else
                        polygons.Add(new GeoPolygon(ReadRingList(tagged, hasZ, hasM), mSrid));
                }
                while (ConsumeComma());
                Expect(')');
                return new GeoMultiPolygon(polygons, mSrid);
            }

            private GeoGeometry ParseGeometryCollection()
            {
                var geometries = new List<GeoGeometry>();
                if (ConsumeEmpty())
                    return new GeoGeometryCollection(geometries, mSrid);
                Expect('(');
                do
                {
                    geometries.Add(ParseGeometry());
                }
                while (ConsumeComma());
                Expect(')');
                return new GeoGeometryCollection(geometries, mSrid);
            }

            private List<GeoCoordinate> ReadCoordinateList(bool tagged, bool hasZ, bool hasM)
            {
                var list = new List<GeoCoordinate>();
                Expect('(');
                do
                {
                    list.Add(ReadCoordinate(tagged, hasZ, hasM));
                }
                while (ConsumeComma());
                Expect(')');
                return list;
            }

            private List<List<GeoCoordinate>> ReadRingList(bool tagged, bool hasZ, bool hasM)
            {
                var rings = new List<List<GeoCoordinate>>();
                Expect('(');
                do
                {
                    rings.Add(ReadCoordinateList(tagged, hasZ, hasM));
                }
                while (ConsumeComma());
                Expect(')');
                return rings;
            }

            private GeoCoordinate ReadCoordinate(bool tagged, bool hasZ, bool hasM)
            {
                double x = ReadNumber();
                double y = ReadNumber();
                double z = double.NaN;
                double m = double.NaN;
                if (tagged)
                {
                    if (hasZ)
                        z = ReadNumber();
                    if (hasM)
                        m = ReadNumber();
                }
                else if (IsNumberStart(PeekNonWhitespace()))
                {
                    // Untagged: a 3rd ordinate is Z, a 4th is M (ISO/PostGIS convention).
                    z = ReadNumber();
                    if (IsNumberStart(PeekNonWhitespace()))
                        m = ReadNumber();
                }
                return new GeoCoordinate(x, y, z, m);
            }

            private void ReadDimensionTag(out bool tagged, out bool hasZ, out bool hasM)
            {
                tagged = false;
                hasZ = false;
                hasM = false;
                SkipWhitespace();
                if (mPos >= mText.Length || !IsLetter(mText[mPos]))
                    return;
                int save = mPos;
                string word = ReadWord();
                if (word == "Z")
                {
                    tagged = true;
                    hasZ = true;
                }
                else if (word == "M")
                {
                    tagged = true;
                    hasM = true;
                }
                else if (word == "ZM")
                {
                    tagged = true;
                    hasZ = true;
                    hasM = true;
                }
                else
                {
                    // Not a dimension tag (e.g. EMPTY) — leave it for the caller.
                    mPos = save;
                }
            }

            private int ReadSridValue()
            {
                SkipWhitespace();
                int start = mPos;
                if (mPos < mText.Length && (mText[mPos] == '+' || mText[mPos] == '-'))
                    mPos++;
                while (mPos < mText.Length && mText[mPos] >= '0' && mText[mPos] <= '9')
                    mPos++;
                if (mPos == start)
                    throw Error("SRID value expected");
                string token = mText.Substring(start, mPos - start);
                if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    throw Error($"invalid SRID value '{token}'");
                return value;
            }

            private double ReadNumber()
            {
                SkipWhitespace();
                int start = mPos;
                while (mPos < mText.Length)
                {
                    char c = mText[mPos];
                    if ((c >= '0' && c <= '9') || c == '.' || c == '+' || c == '-' || c == 'e' || c == 'E')
                        mPos++;
                    else
                        break;
                }
                if (mPos == start)
                    throw Error("number expected");
                string token = mText.Substring(start, mPos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error($"invalid number '{token}'");
                return value;
            }

            private string ReadWord()
            {
                SkipWhitespace();
                int start = mPos;
                while (mPos < mText.Length && IsLetter(mText[mPos]))
                    mPos++;
                if (mPos == start)
                    throw Error("geometry keyword expected");
                return mText.Substring(start, mPos - start).ToUpperInvariant();
            }

            private bool ConsumeEmpty()
            {
                SkipWhitespace();
                if (mPos < mText.Length && IsLetter(mText[mPos]))
                {
                    string word = ReadWord();
                    if (word == "EMPTY")
                        return true;
                    throw Error($"expected 'EMPTY' or '(' but found '{word}'");
                }
                return false;
            }

            private bool ConsumeComma()
            {
                SkipWhitespace();
                if (mPos < mText.Length && mText[mPos] == ',')
                {
                    mPos++;
                    return true;
                }
                return false;
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (mPos >= mText.Length || mText[mPos] != expected)
                    throw Error($"'{expected}' expected");
                mPos++;
            }

            private char PeekNonWhitespace()
            {
                SkipWhitespace();
                return mPos < mText.Length ? mText[mPos] : '\0';
            }

            private void SkipWhitespace()
            {
                while (mPos < mText.Length && char.IsWhiteSpace(mText[mPos]))
                    mPos++;
            }

            public void EnsureAtEnd()
            {
                SkipWhitespace();
                if (mPos != mText.Length)
                    throw Error("unexpected trailing characters");
            }

            private static bool IsLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

            private static bool IsNumberStart(char c)
                => (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.';

            private GeoFormatException Error(string message)
                => new GeoFormatException($"WKT parse error at position {mPos}: {message}.");
        }
    }
}
