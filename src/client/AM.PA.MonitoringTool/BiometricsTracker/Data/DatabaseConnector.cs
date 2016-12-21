﻿// Created by Sebastian Mueller (smueller@ifi.uzh.ch) from the University of Zurich
// Created: 2016-12-07
// 
// Licensed under the MIT License.

using System;
using Shared;
using Shared.Data;
using System.Data;
using System.Collections.Generic;
using System.Globalization;
using BluetoothLowEnergy;

namespace BiometricsTracker.Data
{
    public class DatabaseConnector
    {
        private const string ID = "id";
        private const string TIME = "time";
        private const string HEARTRATE = "heartrate";
        private const string RRINTERVAL = "rr";
        private const string DIFFERENCE_RRINTERVAL = "rrdifference";
        
        private static readonly string CREATE_QUERY = "CREATE TABLE IF NOT EXISTS " + Settings.TABLE_NAME + " (" + ID + " INTEGER PRIMARY KEY, " + TIME + " TEXT, " + HEARTRATE + " INTEGER, " + RRINTERVAL + " DOUBLE, " + DIFFERENCE_RRINTERVAL + " DOUBLE)";
        private static readonly string INSERT_QUERY = "INSERT INTO " + Settings.TABLE_NAME + "(" + TIME + ", " + HEARTRATE + ", " + RRINTERVAL + ", " + DIFFERENCE_RRINTERVAL + ") VALUES ('{0}', {1}, {2}, {3})";
        private static readonly string INSERT_QUERY_MULTIPLE_VALUES = "INSERT INTO " + Settings.TABLE_NAME + " SELECT null AS " + ID + ", " + "'{0}' AS " + TIME + ", {1} AS " + HEARTRATE + ", {2} AS " + RRINTERVAL + ", {3} AS " + DIFFERENCE_RRINTERVAL;

        #region create
        internal static void CreateBiometricTables()
        {
            try
            {
                Database.GetInstance().ExecuteDefaultQuery(CREATE_QUERY);
            }
            catch (Exception e)
            {
                Logger.WriteToLogFile(e);
            }
        }
        #endregion

        #region insert
        internal static void AddHeartMeasurementToDatabase(String timestamp, double heartrate, double rrInterval, double rrDifference)
        {
            Console.WriteLine(rrDifference.ToString());
            try
            {
                string query = string.Empty;
                query += String.Format(INSERT_QUERY, timestamp, Double.IsNaN(heartrate) ? "null" : heartrate.ToString(), rrInterval, Double.IsNaN(rrDifference) ? "null" : rrDifference.ToString());
                Database.GetInstance().ExecuteDefaultQuery(query);
            }
            catch (Exception e)
            {
                Logger.WriteToLogFile(e);
            }
        }
        
        internal static void AddHeartMeasurementsToDatabase(List<HeartRateMeasurement> measurements)
        {
            try
            {
                if (measurements.Count == 0)
                {
                    return;
                }
                else if (measurements.Count == 1)
                {
                    string query = string.Empty;
                    query += String.Format(INSERT_QUERY, measurements[0].Timestamp, Double.IsNaN(measurements[0].HeartRateValue) ? "null" : measurements[0].HeartRateValue.ToString(), measurements[0].RRInterval, Double.IsNaN(measurements[0].RRDifference) ? "null" : measurements[0].RRDifference.ToString());
                    Database.GetInstance().ExecuteDefaultQuery(query);
                }
                else
                {
                    string query = string.Empty;
                    query += String.Format(INSERT_QUERY_MULTIPLE_VALUES, measurements[0].Timestamp, Double.IsNaN(measurements[0].HeartRateValue) ? "null" : measurements[0].HeartRateValue.ToString(), measurements[0].RRInterval, Double.IsNaN(measurements[0].RRDifference) ? "null" : measurements[0].RRDifference.ToString());

                    for (int i = 1; i < measurements.Count; i++)
                    {
                        query += String.Format(" UNION ALL SELECT null, '{0}', {1}, {2}, {3}", measurements[i].Timestamp, Double.IsNaN(measurements[i].HeartRateValue) ? "null" : measurements[i].HeartRateValue.ToString(), measurements[i].RRInterval, Double.IsNaN(measurements[i].RRDifference) ? "null" : measurements[i].RRDifference.ToString());
                    }

                    Logger.WriteToConsole(query);

                    Database.GetInstance().ExecuteDefaultQuery(query);
                }
            }
            catch (Exception e)
            {
                Logger.WriteToLogFile(e);
            }
        }
        #endregion

        #region day
        internal static List<Tuple<DateTime, double, double>> GetBiometricValuesForDay(DateTimeOffset date)
        {
            List<Tuple<DateTime, double, double>> result = new List<Tuple<DateTime, double, double>>();

            var query = "SELECT " + "STRFTIME('%Y-%m-%d %H:%M', " + TIME + ")" + ", AVG(" + HEARTRATE + "), AVG(" + DIFFERENCE_RRINTERVAL + "*" + DIFFERENCE_RRINTERVAL + ") FROM " + Settings.TABLE_NAME + " WHERE " + Database.GetInstance().GetDateFilteringStringForQuery(VisType.Day, date, TIME) + "GROUP BY strftime('%H:%M', " + TIME + ");";
            var table = Database.GetInstance().ExecuteReadQuery(query);

            foreach (DataRow row in table.Rows)
            {
                var timestamp = (String)row[0];
                
                double hr = Double.NaN;
                double.TryParse(row[1].ToString(), out hr);
                if (IsAboveThresholdValue(hr, HEARTRATE))
                {
                    hr = Double.NaN;
                }

                double rmssd = Double.NaN;
                if (row[2] != DBNull.Value)
                {
                    double.TryParse(row[2].ToString(), out rmssd);
                    if (IsAboveThresholdValue(rmssd, DIFFERENCE_RRINTERVAL))
                    {
                        rmssd = Double.NaN;
                    }
                    if (!Double.IsNaN(rmssd))
                    {
                        rmssd = Math.Sqrt(rmssd);
                        rmssd *= 1000;
                    }
                }
                result.Add(new Tuple<DateTime, double, double>(DateTime.ParseExact(timestamp, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), hr, rmssd));
            }
            table.Dispose();

            return result;
        }
        #endregion

        #region week
        internal static List<Tuple<DateTime, double>> GetHRVValuesForWeek(DateTimeOffset date)
        {
            return GetBiometricValuesForWeek(date, RRINTERVAL);
        }

        internal static List<Tuple<DateTime, double>> GetHRValuesForWeek(DateTimeOffset date)
        {
            return GetBiometricValuesForWeek(date, HEARTRATE);
        }

        internal static List<Tuple<DateTime, double>> GetRMSSDValuesForWeek(DateTimeOffset date)
        {
            var result = new List<Tuple<DateTime, double>>();
            
            //Go back to Monday
            while (date.DayOfWeek != DayOfWeek.Monday)
            {
                date = date.AddDays(-1);
            }

            //Iterate over whole week
            while (date.DayOfWeek != DayOfWeek.Sunday)
            {
                result.AddRange(CalculateHourAverage(GetBiometricValuesForDay(date)));
                date = date.AddDays(1);
            }

            //Iterate Sunday
            result.AddRange(CalculateHourAverage(GetBiometricValuesForDay(date)));
            
            return result;
        }

        private static List<Tuple<DateTime, double>> CalculateHourAverage(List<Tuple<DateTime, double, double>> values)
        {
            var result = new List<Tuple<DateTime, double>>();
            
            if (values.Count > 0)
            {
                DateTime firstHour = values[0].Item1;
                DateTime lastHour = values[values.Count - 1].Item1;
                
                while (firstHour.Hour.CompareTo(lastHour.Hour + 1) != 0)
                {
                    List<Tuple<DateTime, double, double>> tuplesForThisHour = values.FindAll(t => t.Item1.Hour.CompareTo(firstHour.Hour) == 0);
                    
                    double sum = 0;
                    double count = 0;

                    foreach (Tuple<DateTime, double, double> t in tuplesForThisHour)
                    {
                        if (!Double.IsNaN(t.Item3))
                        {
                            sum += t.Item3;
                            count++;
                        }
                    }

                    Tuple<DateTime, double> createTuple = new Tuple<DateTime, double>(firstHour, sum / count);
                    result.Add(createTuple);

                    firstHour = firstHour.AddHours(1);
                }
            }
            return result;
        }

        private static List<Tuple<DateTime, double>> GetBiometricValuesForWeek(DateTimeOffset date, String column)
        {
            var result = new List<Tuple<DateTime, double>>();

            var query = "SELECT strftime('%Y-%m-%d %H'," + TIME + "), avg(" + column + ") FROM " + Settings.TABLE_NAME + " WHERE " + Database.GetInstance().GetDateFilteringStringForQuery(VisType.Week, date, TIME) + "AND " + column + " <= " + GetThresholdValue(column) + " GROUP BY strftime('%Y-%m-%d %H',time);";
            var table = Database.GetInstance().ExecuteReadQuery(query);

            foreach (DataRow row in table.Rows)
            {
                var timestamp = (String)row[0];
                double value = Double.NaN;
                if (row[1] != DBNull.Value)
                {
                   double.TryParse(row[1].ToString(), out value);
                }
                result.Add(new Tuple<DateTime, double>(DateTime.ParseExact(timestamp, "yyyy-MM-dd H", CultureInfo.InvariantCulture), value));
            }
            table.Dispose();

            return result;
        }

        private static double GetThresholdValue(string column)
        {
            switch(column)
            {
                case HEARTRATE:
                    return Settings.HEARTRATE_THRESHOLD;

                case RRINTERVAL:
                    return Settings.RR_INTERVAL_THRESHOLD;

                case DIFFERENCE_RRINTERVAL:
                    return Settings.RR_DIFFERENCE_THRESHOLD;
            }
            return Double.MaxValue;
        }

        private static bool IsAboveThresholdValue(double value, string column)
        {
            switch (column)
            {
                case HEARTRATE:
                    return value > Settings.HEARTRATE_THRESHOLD;

                case RRINTERVAL:
                    return value > Settings.RR_INTERVAL_THRESHOLD;

                case DIFFERENCE_RRINTERVAL:
                    return value > Settings.RR_DIFFERENCE_THRESHOLD;
            }
            return false;
        }

        #endregion
    }
}