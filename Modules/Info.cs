﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Models;
using ELO.Services;
using RavenBOT.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELO.Modules
{
    [RavenRequireContext(ContextType.Guild)]
    public partial class Info : ReactiveBase
    {

        [Command("Ranks", RunMode = RunMode.Async)]
        [Summary("Displays information about the server's current ranks")]
        public async Task ShowRanksAsync()
        {
            using (var db = new Database())
            {
                var comp = db.GetOrCreateCompetition(Context.Guild.Id);
                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToList();
                if (ranks.Count == 0)
                {
                    await SimpleEmbedAsync("There are currently no ranks set up.", Color.Blue);
                    return;
                }

                var msg = ranks.OrderByDescending(x => x.Points).Select(x => $"{MentionUtils.MentionRole(x.RoleId)} - ({x.Points}) W: (+{x.WinModifier ?? comp.DefaultWinModifier}) L: (-{x.LossModifier ?? comp.DefaultLossModifier})").ToArray();
                await SimpleEmbedAsync(string.Join("\n", msg), Color.Blue);
            }
        }

        [Command("Profile", RunMode = RunMode.Async)] // Please make default command name "Stats"
        [Alias("Info", "GetUser")]
        [Summary("Displays information about you or the specified user.")]
        public async Task InfoAsync(SocketGuildUser user = null)
        {
            if (user == null)
            {
                user = Context.User as SocketGuildUser;
            }

            using (var db = new Database())
            {
                var player = db.Players.Find(Context.Guild.Id, user.Id);
                if (player == null)
                {
                    if (user.Id == Context.User.Id)
                    {
                        await SimpleEmbedAsync("You are not registered.", Color.DarkBlue);
                    }
                    else
                    {
                        await SimpleEmbedAsync("That user is not registered.", Color.Red);
                    }
                    return;
                }

                var ranks = db.Ranks.Where(x => x.GuildId == Context.Guild.Id).ToList();
                var maxRank = ranks.Where(x => x.Points < player.Points).OrderByDescending(x => x.Points).FirstOrDefault();
                string rankStr = null;
                if (maxRank != null)
                {
                    rankStr = $"Rank: {MentionUtils.MentionRole(maxRank.RoleId)} ({maxRank.Points})\n";
                }

                await SimpleEmbedAsync($"{player.GetDisplayNameSafe()} Stats\n" + // Use Title?
                            $"Points: {player.Points}\n" +
                            rankStr +
                            $"Wins: {player.Wins}\n" +
                            $"Losses: {player.Losses}\n" +
                            $"Draws: {player.Draws}\n" +
                            $"Games: {player.Games}\n" +
                            $"Registered At: {player.RegistrationDate.ToString("dd MMM yyyy")} {player.RegistrationDate.ToShortTimeString()}", Color.Blue);
            }

            //TODO: Add game history (last 5) to this response
            //+ if they were on the winning team?
            //maybe only games with a decided result should be shown?
        }

        [Command("Leaderboard", RunMode = RunMode.Async)]
        [Alias("lb", "top20")]
        [Summary("Shows the current server-wide leaderboard.")]
        //TODO: Ratelimiting as this is a data heavy command.
        public async Task LeaderboardAsync()
        {
            using (var db = new Database())
            {
                //TODO: Implement sort modes

                //Retrieve players in the current guild from database
                var users = db.Players.Where(x => x.GuildId == Context.Guild.Id);

                //Order players by score and then split them into groups of 20 for pagination
                var userGroups = users.OrderByDescending(x => x.Points).SplitList(20).ToArray();
                if (userGroups.Length == 0)
                {
                    await SimpleEmbedAsync("There are no registered users in this server yet.", Color.Blue);
                    return;
                }

                //Convert the groups into formatted pages for the response message
                var pages = GetPages(userGroups);

                //Construct a paginated message with each of the leaderboard pages
                await PagedReplyAsync(new ReactivePager(pages).ToCallBack().WithDefaultPagerCallbacks());
            }
        }

        public List<ReactivePage> GetPages(IEnumerable<Player>[] groups)
        {
            //Start the index at 1 because we are ranking players here ie. first place.
            int index = 1;
            var pages = new List<ReactivePage>(groups.Length);
            foreach (var group in groups)
            {
                var playerGroup = group.ToArray();
                var lines = GetPlayerLines(playerGroup, index);
                index = lines.Item1;
                var page = new ReactivePage();
                page.Color = Color.Blue;
                page.Title = $"{Context.Guild.Name} - Leaderboard";
                page.Description = lines.Item2;
                pages.Add(page);
            }

            return pages;
        }

        //Returns the updated index and the formatted player lines
        public (int, string) GetPlayerLines(Player[] players, int startValue)
        {
            var sb = new StringBuilder();

            //Iterate through the players and add their summary line to the list.
            foreach (var player in players)
            {
                sb.AppendLine($"{startValue}: {player.GetDisplayNameSafe()} - `{player.Points}`");
                startValue++;
            }

            //Return the updated start value and the list of player lines.
            return (startValue, sb.ToString());
        }
    }
}
