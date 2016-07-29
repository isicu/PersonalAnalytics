﻿// Created by André Meyer (ameyer@ifi.uzh.ch) from the University of Zurich
// Created: 2016-06-20
// 
// Licensed under the MIT License.

using MsOfficeTracker.Data;
using Shared;
using Shared.Helpers;
using System;

namespace MsOfficeTracker.Visualizations
{
    public class DayEmailsTable : BaseVisualization, IVisualization
    {
        private readonly DateTimeOffset _date;

        public DayEmailsTable(DateTimeOffset date)
        {
            this._date = date;

            Title = "Email Stats";
            IsEnabled = true; //todo: handle by user
            Order = 6; //todo: handle by user
            Size = VisSize.Square;
            Type = VisType.Day;
        }

        public override string GetHtml()
        {
            var html = string.Empty;

            /////////////////////
            // fetch data sets
            /////////////////////

            // get the latest stored email entry
            var emailsSnapshotResult = Queries.GetEmailsSnapshot(_date);
            var inbox = emailsSnapshotResult.Item2;
            var inboxUnread = emailsSnapshotResult.Item3;
            var sent = emailsSnapshotResult.Item4;
            var received = emailsSnapshotResult.Item5;
            var receivedUnread = emailsSnapshotResult.Item6;

            var isToday = (_date.Date == DateTime.Now.Date);
            var lastUpdatedMinsAgo = Math.Abs((DateTime.Now - emailsSnapshotResult.Item1).TotalMinutes);

            // if database entry is outdated or not there, create a live API call and override entries
            if (emailsSnapshotResult.Item1 == DateTime.MinValue || // no emails stored yet
                (isToday && lastUpdatedMinsAgo > Settings.SaveEmailCountsIntervalInMinutes) || // request is for today and saved results are too old
                (sent == -1) || (received == -1) || (receivedUnread == -1)) // could not fetch sent emails
            {
                // create and save a new email snapshot (inbox, sent, received)
                var res = Queries.CreateEmailsSnapshot(_date.Date, false);
                inbox = res.Item1;
                inboxUnread = res.Item2;
                sent = res.Item3;
                received = res.Item4;
                receivedUnread = res.Item5;
            }

            // error (only if no data at all)
            if (inbox == -1 && inbox == -1 && sent == -1 && received == -1 && receivedUnread == -1)
            {
                return VisHelper.NotEnoughData(Dict.NotEnoughData);
            }

            // as a goodie get this too :)
            //var timeSpentInOutlook = Queries.TimeSpentInOutlook(_date);


            /////////////////////
            // HTML
            /////////////////////

            //var emailInboxString = (inboxSize == -1) ? "?" : inboxSize.ToString(CultureInfo.InvariantCulture);
            //html += "<p style='text-align: center; margin-top:-0.7em;'><strong style='font-size:2.5em;'>" + emailInboxString + "</strong></p>";

            html += "<table>";
            if (sent > -1) html += "<tr><td><strong style='font-size:2.5em; color:#007acc;'>" + sent + "</strong></td><td>emails sent today</td></tr>";
            if (received > -1 && receivedUnread > -1) html += "<tr><td><strong style='font-size:2.5em; color:#007acc;'>" + (received - receivedUnread) + "</strong></td><td>emails received and read today</td></tr>";
            if (receivedUnread > -1) html += "<tr><td><strong style='font-size:2.5em; color:#007acc;'>" + receivedUnread + "</strong></td><td>emails received today and currently unread</td></tr>";
            if (inbox > -1) html += "<tr><td><strong style='font-size:2.5em; color:#007acc;'>" + inbox + "</strong></td><td>emails in your inbox</td></tr>";
            if (inboxUnread > -1) html += "<tr><td><strong style='font-size:2.5em; color:#007acc;'>" + inboxUnread + "</strong></td><td>unread emails in your inbox</td></tr>";
            html += "</table>";
        
            //if (timeSpentInOutlook > 1)
            //{
            //    html += "<p style='text-align: center; margin-top:-0.7em;'>time spent in Outlook: " + Math.Round(timeSpentInOutlook, 0) + "min</p>";
            //}

            return html;
        }
    }
}