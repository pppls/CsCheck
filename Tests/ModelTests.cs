﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using CsCheck;
using Xunit;

namespace Tests
{
    public class ModelTests
    {
        public enum Currency { EUR, GBP, USD, CAD };
        public enum Country { DE, GB, US, CA };
        public enum Exchange { LMAX, EQTA, GLPS, XCNQ }
        public class Instrument
        {
            public string Name { get; }
            public Country Country { get; }
            public Currency Currency { get; }
            public Instrument(string name, Country country, Currency currency) { Name = name; Country = country; Currency = currency; }
            public override bool Equals(object o) => o is Instrument i && i.Name == Name && i.Country == Country && i.Currency == Currency;
            public override int GetHashCode() => HashCode.Combine(Name, Country, Currency);
        }
        public class Equity : Instrument
        {
            public IReadOnlyCollection<Exchange> Exchanges { get; }
            public Equity(string name, Country country, Currency currency, IReadOnlyCollection<Exchange> exchanges) : base(name, country, currency)
            {
                Exchanges = exchanges;
            }
            public override bool Equals(object o) => o is Equity i && base.Equals(i) && i.Exchanges.SequenceEqual(Exchanges);
            public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Exchanges);
        }
        public class Bond : Instrument
        {
            public IReadOnlyDictionary<DateTime, double> Coupons { get; }
            public Bond(string name, Country country, Currency currency, IReadOnlyDictionary<DateTime, double> coupons) : base(name, country, currency)
            {
                Coupons = coupons;
            }
            public override bool Equals(object o) => o is Bond i && base.Equals(i) && i.Coupons.SequenceEqual(Coupons);
            public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Coupons);
        }
        public class Trade
        {
            public DateTime Date { get; }
            public int Quantity { get; }
            public double Cost { get; }
            public Trade(DateTime date, int quantity, double cost) { Date = date; Quantity = quantity; Cost = cost; }
            public override bool Equals(object o) => o is Trade i && i.Date == Date && i.Quantity == Quantity && i.Cost == Cost;
            public override int GetHashCode() => HashCode.Combine(Date, Quantity, Cost);
        }
        public class Position
        {
            public Instrument Instrument { get; }
            public IReadOnlyList<Trade> Trades { get; }
            public double Price { get; }
            public Position(Instrument instrument, IReadOnlyList<Trade> trades, double price) { Instrument = instrument; Trades = trades; Price = price; }
            public int Quantity => Trades.Sum(i => i.Quantity);
            public double Cost => Trades.Sum(i => i.Cost);
            public double Profit => Price * Quantity - Cost;
            public override bool Equals(object o) => o is Position i && i.Instrument == Instrument && i.Trades.SequenceEqual(Trades) && i.Price == Price;
            public override int GetHashCode() => HashCode.Combine(Instrument, Trades, Price);
        }
        public class Portfolio
        {
            public string Name { get; }
            public Currency Currency { get; }
            public IReadOnlyCollection<Position> Positions { get; }
            public Portfolio(string name, Currency currency, IReadOnlyCollection<Position> positions) { Name = name; Currency = currency; Positions = positions; }
            public override bool Equals(object o) => o is Portfolio i && i.Name == Name && i.Currency == Currency && i.Positions.SequenceEqual(Positions);
            public override int GetHashCode() => HashCode.Combine(Name, Currency, Positions);
        }

        public static class ModelGen
        {
            public static Gen<string> Name = Gen.String["ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 "];
            public static Gen<Currency> Currency = Gen.Enum<Currency>();
            public static Gen<Country> Country = Gen.Enum<Country>();
            public static Gen<int> Quantity = Gen.Int[-99, 99].Select(Gen.Int[0, 5]).Select(t => t.V0 * (int)Math.Pow(10, t.V1));
            public static Gen<double> Coupon = Gen.Int[0, 100].Select(i => i * 0.125);
            public static Gen<double> Price = Gen.Int[0001, 9999].Select(i => i * 0.01);
            public static Gen<DateTime> Date = Gen.DateTime[new DateTime(2000, 1, 1), DateTime.Today].Select(i => i.Date);
            public static Gen<Equity> Equity = Gen.Select(Name, Country, Currency, Gen.Enum<Exchange>().HashSet[1, 3], (n, co, cu, e) => new Equity(n, co, cu, e));
            public static Gen<Bond> Bond = Gen.Select(Name, Country, Currency, Gen.SortedDictionary(Date, Coupon), (n, co, cu, c) => new Bond(n, co, cu, c));
            public static Gen<Instrument> Instrument = Gen.Frequency((2, Equity.Cast<Instrument>()), (1, Bond.Cast<Instrument>()));
            public static Gen<Trade> Trade = Gen.Select(Date, Quantity, Price, (dt, q, p) => new Trade(dt, q, q * p));
            public static Gen<Position> Position = Gen.Select(Instrument, Trade.List, Price, (i, t, p) => new Position(i, t, p));
            public static Gen<Portfolio> Portfolio = Gen.Select(Name, Currency, Position.Array, (n, c, p) => new Portfolio(n, c, p));
        }


        [Fact]
        public void Model()
        {
            ModelGen.Portfolio.Regression(p => p.Positions.Count == 5,
                "20c40eec8f1734032", p => p.Positions.Sum(i => i.Profit) == -527_314_004.03999966);
        }

        // T -> (byte[],int) -> (byte[],int)
        // Regression Gen tests, extension of HashCode? Where and codepath for examples
        // HashStream
        // Serial on type?
    }
    public struct Ser
    {
        public byte[] Bytes;
        public int Position;
        //public Ser()
        //{
        //    Bytes = ArrayPool<byte>.Shared.Rent(128);
        //    Position = 0;
        //}
        public void Resize(int i)
        {
        }
    }

    public static class SerEx
    {
        public static Ser WriteInt(this ref Ser s, int x)
        {
            if(x < 0x80u)
            {
                s.Resize(1);
                s.Bytes[s.Position++] = (byte)x;
            }
            return s;
        }
        public static int ReadInt(this ref Ser s)
        {
            return 1;
        }
    }
}