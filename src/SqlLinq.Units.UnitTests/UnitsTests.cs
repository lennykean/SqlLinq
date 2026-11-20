using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlLinq.SyntaxTree.Expressions;
using SqlLinq.TimeSeries;
using UnitsNet;
using UnitsNet.Metadata;
using UnitsNet.Units;

namespace SqlLinq.Units.UnitTests
{
    [TestClass]
    public class UnitsTests
    {
        private static readonly List<Frame> Frames =
        [
            new Frame { Time = 0, CoreTemp = 20, PlasmaPressure = 30, DriveSpin = 800, Scan = 1 },
            new Frame { Time = 1, CoreTemp = 60, PlasmaPressure = 100, DriveSpin = 3000, Scan = 2 },
            new Frame { Time = 2, CoreTemp = 85, PlasmaPressure = 150, DriveSpin = 6500, Scan = 3 },
            new Frame { Time = 3, CoreTemp = 95, PlasmaPressure = 101.3, DriveSpin = 7000, Scan = 4 },
        ];

        private static readonly UnitConversions Units = new UnitConversions(new Frame().GetObjectMetadata(CultureInfo.InvariantCulture).Values);

        private const double PsiPerKpa = 0.14503773773;

        private static IList<IDictionary<string, object>> Run(string nrql)
        {
            return new TimeSeriesQuery<Frame>(nrql, f => TimeSpan.FromSeconds(f.Time), Units.Expand, [Units]).Evaluate(Frames);
        }

        private static ExpressionNode Parse(string expression, UnitConversions units)
        {
            return (ExpressionNode)SqlSyntaxTree.ParseExpression(expression, [units]).Root;
        }

        private static double Max(string expression)
        {
            return (double)Run("SELECT max(" + expression + ") AS M FROM shipslog").Single()["M"];
        }

        private static void Fails(string expression, string message)
        {
            var e = Assert.ThrowsExactly<SqlException>(() => Run("SELECT max(" + expression + ") FROM shipslog"));
            Assert.Contains(message, e.Message);
        }

        [TestMethod]
        public void ColumnConvertsFromItsOwnUnit()
        {
            // Act
            var fahrenheit = Max("convert([coretemp], '°F')");
            var revolutionsPerSecond = Max("convert(DriveSpin, 'r/s')");

            // Assert
            Assert.AreEqual(95 * 1.8 + 32, fahrenheit, 1e-9);
            Assert.AreEqual(7000 / 60.0, revolutionsPerSecond, 1e-9);
        }

        [TestMethod]
        [DataRow("CONVERT")]
        [DataRow("Convert")]
        public void ConvertNameIsCaseInsensitive(string name)
        {
            // Act
            var fahrenheit = Max(name + "(CoreTemp, '°F')");

            // Assert
            Assert.AreEqual(95 * 1.8 + 32, fahrenheit, 1e-9);
        }

        [TestMethod]
        [DataRow("Temperature.DegreeFahrenheit")]
        [DataRow("DegreeFahrenheit")]
        [DataRow("degreefahrenheit")]
        [DataRow("°F")]
        [DataRow("Temperature.°F")]
        public void ColumnUnitsByIdNameOrAbbreviation(string unit)
        {
            // Act
            var fahrenheit = Max("convert(CoreTemp, '" + unit + "')");

            // Assert
            Assert.AreEqual(95 * 1.8 + 32, fahrenheit, 1e-9);
        }

        [TestMethod]
        public void AllowUnitConversionLimitsTheTargets()
        {
            // Act
            var psi = Max("convert(PlasmaPressure, 'psi')");
            var bar = Max("convert(PlasmaPressure, 'bar')");

            // Assert
            Assert.AreEqual(150 * PsiPerKpa, psi, 1e-6);
            Assert.AreEqual(1.5, bar, 1e-12);

            // Act & Assert
            Fails("convert(PlasmaPressure, 'mbar')", "PlasmaPressure (Kilopascal) can't convert to 'mbar'. Its units are kPa, psi, bar.");
        }

        [TestMethod]
        public void ColumnConvertsToItsOwnUnitEvenIfItsConversionsLeaveItOut()
        {
            // Arrange
            var plasmaPressure = new Frame().GetObjectMetadata(CultureInfo.InvariantCulture)["PlasmaPressure"];
            var units = new UnitConversions([new QuantityMetadata(plasmaPressure.Property, plasmaPressure.Unit, plasmaPressure.Conversions.Skip(1).ToList())]);

            // Act
            var expanded = units.Expand(Parse("convert(PlasmaPressure, 'kPa')", units));

            // Assert
            Assert.AreEqual("((PlasmaPressure) * 1.0 + 0.0)", expanded.ToSql());
        }

        [TestMethod]
        public void ThreeArgumentsOnAColumnMustStateItsUnit()
        {
            // Act
            var fahrenheit = Max("convert(CoreTemp, '°C', '°F')");

            // Assert
            Assert.AreEqual(95 * 1.8 + 32, fahrenheit, 1e-9);

            // Act & Assert
            Fails("convert(CoreTemp, '°F', '°C')", "CoreTemp can't be converted from °F because it's in DegreeCelsius.");
        }

        [TestMethod]
        public void BareValuesMustNameTheirKind()
        {
            // Arrange
            var scan = new QuantityMetadata(typeof(Frame).GetProperty(nameof(Frame.Scan)), null, []);
            var units = new UnitConversions([scan]);

            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => units.Expand(Parse("convert(Scan, 'psi')", units)));
            Assert.Contains("can't be converted without unit metadata or a from unit", e.Message);
            Fails("convert(CoreTemp * 2, '°F')", "can't be converted without unit metadata or a from unit");
        }

        [TestMethod]
        public void KindIsNeverGuessed()
        {
            // Act & Assert
            Fails("convert(Scan, 'DegreeCelsius', 'DegreeFahrenheit')", "Scan has no unit metadata, so 'DegreeCelsius' can't be converted without its kind: 'Temperature.DegreeCelsius' or 'TemperatureDelta.DegreeCelsius'.");
        }

        [TestMethod]
        public void BareValueWithAKind()
        {
            // Act
            var psiFromAbbreviation = Max("convert(Scan, 'Pressure.kPa', 'psi')");
            var psiFromName = Max("convert(Scan, 'Pressure.Kilopascal', 'Pressure.psi')");
            var fahrenheit = Max("convert(Scan, 'Temperature.DegreeCelsius', 'DegreeFahrenheit')");
            var fahrenheitDelta = Max("convert(Scan, 'TemperatureDelta.DegreeCelsius', 'DegreeFahrenheit')");

            // Assert
            Assert.AreEqual(4 * PsiPerKpa, psiFromAbbreviation, 1e-9);
            Assert.AreEqual(4 * PsiPerKpa, psiFromName, 1e-9);
            Assert.AreEqual(4 * 1.8 + 32, fahrenheit, 1e-9);
            Assert.AreEqual(4 * 1.8, fahrenheitDelta, 1e-9);
        }

        [TestMethod]
        public void KindAppliesToBothUnits()
        {
            // Act & Assert
            Fails("convert(Scan, 'Pressure.kPa', 'Temperature.°F')", "'Pressure.kPa' can't be converted to Temperature because it's Pressure.");
            Fails("convert(Scan, 'Pressure.kPa', '°F')", "'°F' can't be used because it names no Pressure unit or more than one.");
            Fails("convert(Scan, 'Pressure.nonsense', 'psi')", "'nonsense' can't be used because it names no Pressure unit or more than one.");
            Fails("convert(Scan, 'nonsense', 'psi')", "'nonsense' can't be converted because it names no unit or more than one.");
        }

        [TestMethod]
        public void AbbreviationsIgnoreCaseUnlessThatIsAmbiguousAndNamesAlwaysDo()
        {
            // Act
            var millibar = Max("convert(Scan, 'Pressure.bar', 'mbar')");
            var megabar = Max("convert(Scan, 'Pressure.bar', 'Mbar')");
            var milliseconds = Max("convert(Scan, 'duration.second', 'MilliSecond')");
            var psi = Max("convert(Scan, 'Pressure.KPA', 'psi')");

            // Assert
            Assert.AreEqual(4000.0, millibar, 1e-9);
            Assert.AreEqual(4e-6, megabar, 1e-15);
            Assert.AreEqual(4000.0, milliseconds, 1e-9);
            Assert.AreEqual(4 * PsiPerKpa, psi, 1e-9);

            // Act & Assert
            Fails("convert(Scan, 'Pressure.MBAR', 'psi')", "'MBAR' can't be used because it names no Pressure unit or more than one.");
        }

        [TestMethod]
        [DataRow("'Pressure.FootOfElevation', 'bar'")]
        [DataRow("'FuelEfficiency.LiterPer100Kilometers', 'KilometerPerLiter'")]
        [DataRow("'Power.Petawatt', 'Femtowatt'")]
        public void ConversionsThatCantBeExpandedAreRejected(string units)
        {
            // Act & Assert
            Fails("convert(Scan, " + units + ")", "Scan, " + units + " isn't a linear conversion");
        }

        [TestMethod]
        public void DictionaryRowsUseTheMetadataTheyAreGiven()
        {
            // Arrange
            var rows = new double?[] { 20, null, 95 }.Select(coreTemp => (IDictionary<string, object>)new Dictionary<string, object> { ["CoreTemp"] = coreTemp }).ToList();
            var query = new TimeSeriesQuery<IDictionary<string, object>>("SELECT count(*) AS N, count(convert(CoreTemp, '°F')) AS F, max(convert(CoreTemp, '°F')) AS Hottest FROM shipslog WHERE convert(CoreTemp, '°F') > 100 OR CoreTemp IS NULL", r => TimeSpan.Zero, Units.Expand, [Units]);

            // Act
            var row = query.Evaluate(rows).Single();

            // Assert
            Assert.AreEqual(2, row["N"]);
            Assert.AreEqual(1, row["F"]);
            Assert.AreEqual(95 * 1.8 + 32, (double)row["Hottest"], 1e-9);
        }

        [TestMethod]
        public void ExpandWritesInvariantLinearSql()
        {
            // Arrange
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            try
            {
                // Act
                var comparison = Units.Expand(Parse("convert(CoreTemp, '°F') > 150", Units)).ToSql();
                var negativeOffset = Units.Expand(Parse("convert(Scan, 'Temperature.K', '°C')", Units)).ToSql();
                var nested = Units.Expand(Parse("convert(convert(PlasmaPressure, 'bar'), 'Pressure.bar', 'psi')", Units)).ToSql();
                var unchanged = Units.Expand(Parse("CoreTemp * 2 > 150", Units)).ToSql();

                // Assert
                Assert.AreEqual("((CoreTemp) * 1.8 + 32.0) > 150", comparison);
                Assert.AreEqual("((Scan) * 1.0 + (-273.15))", negativeOffset);
                Assert.AreEqual("((((PlasmaPressure) * 0.01 + 0.0)) * 14.5037737730209 + 0.0)", nested);
                Assert.AreEqual("CoreTemp * 2 > 150", unchanged);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestMethod]
        [DataRow("convert(CoreTemp)", "convert can't run because it takes 2 or 3 arguments, not 1.")]
        [DataRow("convert(CoreTemp, 'a', 'b', 'c')", "convert can't run because it takes 2 or 3 arguments, not 4.")]
        [DataRow("convert(CoreTemp, °F)", "The text can't be unquoted because it isn't a quoted string: °F")]
        public void BadCallsThrowSqlException(string expression, string message)
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => Units.Expand(Parse(expression, Units)));
            Assert.IsTrue(e.Message.StartsWith(message, StringComparison.Ordinal), e.Message);
        }

        [TestMethod]
        public void UnitQueryExpandsConvertBeforeQuerying()
        {
            // Act
            var hot = Frames.UnitQuery("SELECT * FROM this WHERE convert(CoreTemp, '°F') > 190", Units);

            // Assert
            CollectionAssert.AreEqual(Frames.Where(f => f.CoreTemp * 1.8 + 32 > 190).ToList(), hot.ToList());
        }

        [TestMethod]
        [DataRow("convert(CoreTemp, '°F')")]
        [DataRow("convert(DriveSpin, 'r/s')")]
        public void ConvertReadsTypedColumnsUnboxed(string expression)
        {
            // Act
            var lambda = SqlExpression.Lambda<Frame>(Parse(expression, Units));
            var call = (System.Linq.Expressions.MethodCallExpression)((System.Linq.Expressions.UnaryExpression)lambda.Body).Operand;

            // Assert
            Assert.AreEqual(typeof(double?), call.Type);
            Assert.AreEqual(typeof(double?), call.Arguments[0].Type);
        }

        [TestMethod]
        public void PipelineRewritesTheTreeBeforeConverting()
        {
            // Arrange
            var condition = Parse("convert(CoreTemp, '°F') > 190", Units);
            var syntax = SqlSyntaxTree.Parse("SELECT * FROM this WHERE Hot", [Units]);

            // Act
            var hot = syntax.Replace(n => n is NodeWithId id && id.Id.LookupId == "Hot" ? condition : null).Compile<Frame, Frame>().Execute(Frames);

            // Assert
            CollectionAssert.AreEqual(Frames.Where(f => f.CoreTemp * 1.8 + 32 > 190).ToList(), hot.ToList());
        }

        private static double MaxAsFunction(string expression)
        {
            var functions = new ISqlFunction[] { Units };

            return (double)new TimeSeriesQuery<Frame>("SELECT max(" + expression + ") AS M FROM shipslog", f => TimeSpan.FromSeconds(f.Time), null, functions).Evaluate(Frames).Single()["M"];
        }

        [TestMethod]
        public void ConvertAsAFunctionMatchesExpand()
        {
            // Act
            var fahrenheit = MaxAsFunction("convert(CoreTemp, '°F')");
            var psi = MaxAsFunction("convert(Scan, 'Pressure.kPa', 'psi')");
            var nested = MaxAsFunction("convert(convert(PlasmaPressure, 'bar'), 'Pressure.bar', 'psi')");

            // Assert
            Assert.AreEqual(Max("convert(CoreTemp, '°F')"), fahrenheit, 1e-9);
            Assert.AreEqual(Max("convert(Scan, 'Pressure.kPa', 'psi')"), psi, 1e-9);
            Assert.AreEqual(Max("convert(convert(PlasmaPressure, 'bar'), 'Pressure.bar', 'psi')"), nested, 1e-9);
        }

        [TestMethod]
        public void ConvertAsAFunctionSupportsNonLinearUnits()
        {
            // Act
            var bar = MaxAsFunction("convert(Scan, 'Pressure.FootOfElevation', 'bar')");
            var kilometersPerLiter = MaxAsFunction("convert(Scan, 'FuelEfficiency.LiterPer100Kilometers', 'KilometerPerLiter')");

            // Assert
            Assert.AreEqual(Frames.Max(f => UnitConverter.Convert(f.Scan, PressureUnit.FootOfElevation, PressureUnit.Bar)), bar, 1e-9);
            Assert.AreEqual(100.0, kilometersPerLiter, 1e-9);
        }

        [TestMethod]
        public void ConvertAsAFunctionReportsOverflowAsSqlException()
        {
            // Act & Assert
            var e = Assert.ThrowsExactly<SqlException>(() => MaxAsFunction("convert(Scan, 'Power.Petawatt', 'Femtowatt')"));
            Assert.Contains("can't convert 1 because the result is out of range.", e.Message);
        }

        [TestMethod]
        public void QualifiedColumnsUseTheirMetadata()
        {
            // Act
            var thisQualified = Max("convert(this.CoreTemp, '°F')");
            var asFunction = MaxAsFunction("convert(this.CoreTemp, '°F')");
            var hot = Frames.UnitQuery("SELECT * FROM this t WHERE convert(t.CoreTemp, '°F') > 190", Units);

            // Assert
            Assert.AreEqual(95 * 1.8 + 32, thisQualified, 1e-9);
            Assert.AreEqual(95 * 1.8 + 32, asFunction, 1e-9);
            CollectionAssert.AreEqual(Frames.Where(f => f.CoreTemp * 1.8 + 32 > 190).ToList(), hot.ToList());
        }

        [TestMethod]
        public void ExpressionsOfColumnsConvertAsBareValues()
        {
            // Act
            var expanded = Units.Expand(Parse("convert(Scan * f.DriveSpin, 'Length.km', 'm')", Units)).ToSql();
            var far = Frames.UnitQuery("SELECT * FROM this f WHERE convert(Scan * f.DriveSpin, 'Length.km', 'm') > 20000000", Units);

            // Assert
            Assert.AreEqual("((Scan * f.DriveSpin) * 1000.0 + 0.0)", expanded);
            CollectionAssert.AreEqual(Frames.Where(f => f.Scan * f.DriveSpin * 1000 > 20000000).ToList(), far.ToList());
        }

        [TestMethod]
        public void AnAmbiguousAbbreviationNamesItsUnits()
        {
            // Act
            var ambiguous = Assert.ThrowsExactly<SqlException>(() => Run("SELECT max(convert(Range, 'pica')) FROM shipslog"));
            var unknown = Assert.ThrowsExactly<SqlException>(() => Run("SELECT max(convert(Range, 'nonsense')) FROM shipslog"));

            // Assert
            Assert.AreEqual("'pica' can't be resolved for Range (Meter) because it could be 'DtpPica' or 'PrinterPica'.", ambiguous.Message);
            Assert.Contains(", DtpPica, ", unknown.Message);
            Assert.Contains(", PrinterPica, ", unknown.Message);
            Assert.DoesNotContain(" pica,", unknown.Message);
        }

        [TestMethod]
        [DataRow("convert(CoreTemp, 'nonsense')")]
        [DataRow("convert(CoreTemp, '°F', '°C')")]
        [DataRow("convert(CoreTemp * 2, '°F')")]
        [DataRow("convert(CoreTemp)")]
        [DataRow("convert(CoreTemp, Scan)")]
        [DataRow("convert(Scan, 'Pressure.nonsense', 'bar')")]
        public void ExpandErrorsAreAtTheirConvert(string call)
        {
            // Arrange
            var nrql = "SELECT max(" + call + ") FROM shipslog";

            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Run(nrql));

            // Assert
            Assert.AreEqual(nrql.IndexOf(call, StringComparison.Ordinal), e.Start);
            Assert.AreEqual(call.Length, e.Length);
        }

        [TestMethod]
        public void AWrongFromUnitIsAFromUnitError()
        {
            // Act & Assert
            Fails("convert(PlasmaPressure, 'Mass.kg', 'psi')", "PlasmaPressure can't be converted from Mass.kg because it's in Kilopascal.");
            Fails("convert(PlasmaPressure, 'mbar', 'psi')", "PlasmaPressure can't be converted from mbar because it's in Kilopascal.");
        }

        [TestMethod]
        public void UnitQuerySupportsNonLinearUnits()
        {
            // Act
            var efficient = Frames.UnitQuery("SELECT * FROM this WHERE convert(Scan, 'FuelEfficiency.LiterPer100Kilometers', 'KilometerPerLiter') > 40", Units);

            // Assert
            CollectionAssert.AreEqual(Frames.Where(f => 100 / f.Scan > 40).ToList(), efficient.ToList());
        }

        [TestMethod]
        [DataRow("SELECT * FROM this WHERE convert(CoreTemp, 'nonsense') > 1", "convert(CoreTemp, 'nonsense')")]
        [DataRow("SELECT convert(CoreTemp) AS F FROM this", "convert(CoreTemp)")]
        public void UnitQueryErrorsAreAtTheirConvert(string sql, string call)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => Frames.UnitQuery<Frame, IDictionary<string, object>>(sql, Units).ToList());

            // Assert
            Assert.AreEqual(sql.IndexOf(call, StringComparison.Ordinal), e.Start);
            Assert.AreEqual(call.Length, e.Length);
        }

        [TestMethod]
        [DataRow("SELECT average(convert(Station, 'Length.m', 'km')) FROM shipslog")]
        [DataRow("SELECT count(*) FROM shipslog WHERE convert(Station, 'Length.m', 'km') > 1")]
        public void ExpandedErrorsQuoteTheConvertAsWritten(string nrql)
        {
            // Act
            var e = Assert.ThrowsExactly<SqlException>(() => new TimeSeriesQuery<Log>(nrql, l => TimeSpan.FromSeconds(l.Time), Units.Expand, [Units]));

            // Assert
            Assert.Contains("convert(Station, 'Length.m', 'km')", e.Message);
            Assert.DoesNotContain("0.001", e.Message);
            Assert.AreEqual(nrql.IndexOf("convert", StringComparison.Ordinal), e.Start);
        }

        [TestMethod]
        public void ConversionsForAnotherRowTypeAreSqlExceptions()
        {
            // Arrange
            var logs = new List<Log> { new Log { CoreTemp = 68 } };

            // Act
            var queried = Assert.ThrowsExactly<SqlException>(() => logs.UnitQuery("SELECT * FROM this WHERE convert(CoreTemp, 'K') > 0", Units).ToList());
            var stated = logs.UnitQuery<Log, IDictionary<string, object>>("SELECT convert(CoreTemp, 'Temperature.°F', 'K') AS K FROM this", Units).Single();

            // Assert
            Assert.AreEqual("CoreTemp can't be converted without unit metadata or a from unit.", queried.Message);
            Assert.AreEqual(293.15, (double)stated["K"], 1e-9);
        }

        [TestMethod]
        public void NonFiniteValuesConvertToThemselves()
        {
            // Arrange
            var values = new object[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity };
            var frames = values.Select(v => new Frame { CoreTemp = (double)v, Range = (double)v }).ToList();
            var series = new TimeSeriesQuery<Frame>("SELECT convert(CoreTemp, '°F') AS F, convert(Range, 'km') AS K FROM shipslog", f => TimeSpan.Zero, Units.Expand, [Units]);

            // Act
            var expanded = series.Evaluate(frames);
            var queried = frames.UnitQuery<Frame, IDictionary<string, object>>("SELECT convert(CoreTemp, '°F') AS F, convert(Range, 'km') AS K FROM this", Units).ToList();
            var cold = frames.UnitQuery("SELECT * FROM this WHERE NOT convert(CoreTemp, '°F') > 0", Units).ToList();

            // Assert
            CollectionAssert.AreEqual(values, expanded.Select(r => r["F"]).ToList());
            CollectionAssert.AreEqual(values, expanded.Select(r => r["K"]).ToList());
            CollectionAssert.AreEqual(values, queried.Select(r => r["F"]).ToList());
            CollectionAssert.AreEqual(values, queried.Select(r => r["K"]).ToList());
            CollectionAssert.AreEqual(new[] { frames[0], frames[2] }, cold);
        }

        [TestMethod]
        public void ResultsOutOfRangeAreSqlExceptions()
        {
            // Arrange
            var frames = new List<Frame> { new Frame { CoreTemp = 1e308, Scan = -5.5 } };

            // Act
            var overflow = Assert.ThrowsExactly<SqlException>(() => frames.UnitQuery<Frame, IDictionary<string, object>>("SELECT convert(CoreTemp, '°F') AS F FROM this", Units).ToList());
            var undefined = Assert.ThrowsExactly<SqlException>(() => frames.UnitQuery<Frame, IDictionary<string, object>>("SELECT convert(Scan, 'Pressure.kPa', 'm of elevation') AS E FROM this", Units).ToList());

            // Assert
            Assert.AreEqual("convert(CoreTemp, '°F') can't convert 1E+308 because the result is out of range.", overflow.Message);
            Assert.AreEqual(SqlErrorKind.Evaluation, overflow.Kind);
            Assert.EndsWith(" because the result is out of range.", undefined.Message);
            Assert.AreEqual(SqlErrorKind.Evaluation, undefined.Kind);
        }

        [TestMethod]
        public void EveryAbbreviationUnitsNetKnowsConverts()
        {
            // Act
            var secondary = Max("convert(PlasmaPressure, 'lb/in²')");
            var padded = Max("convert(CoreTemp, ' °F')");
            var pounds = Max("convert(Scan, 'Mass.lbs', 'kg')");

            // Assert
            Assert.AreEqual(Max("convert(PlasmaPressure, 'psi')"), secondary);
            Assert.AreEqual(Max("convert(CoreTemp, '°F')"), padded);
            Assert.AreEqual(Max("convert(Scan, 'Mass.lb', 'kg')"), pounds);

            // Act & Assert
            Fails("convert(Scan, 'Length.pt', 'm')", "'pt' can't be used because it names no Length unit or more than one.");
        }

        private sealed class Log
        {
            public double Time { get; set; }

            public string Station { get; set; }

            public double CoreTemp { get; set; }
        }
    }
}
