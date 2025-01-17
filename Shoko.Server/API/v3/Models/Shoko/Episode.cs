using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class Episode : BaseModel
    {
        /// <summary>
        /// The relevant IDs for the Episode: Shoko, AniDB, TvDB
        /// </summary>
        public EpisodeIDs IDs { get; set; }

        /// <summary>
        /// Length of the Episode
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// The Last Watched Date for the current user. If null, it is unwatched
        /// </summary>
        [Required]
        public DateTime? Watched { get; set; }

        public Episode() {}

        public Episode(HttpContext ctx, SVR_AnimeEpisode ep)
        {
            var tvdb = ep.TvDBEpisodes;
            IDs = new EpisodeIDs
            {
                ID = ep.AnimeEpisodeID,
                AniDB = ep.AniDB_EpisodeID,
                TvDB = tvdb.Select(a => a.Id).ToList()
            };

            var anidb = ep.AniDB_Episode;
            if (anidb != null)
            {
                Duration = new TimeSpan(0, 0, anidb.LengthSeconds);
            }

            var uid = ctx.GetUser()?.JMMUserID ?? 0;
            Watched = ep.GetVideoLocals().Select(v => v.GetUserRecord(uid)?.WatchedDate).Where(v => v.HasValue).OrderByDescending(v => v).FirstOrDefault();
            Name = ep.Title;

            Size = ep.GetVideoLocals().Count;
        }

        private static EpisodeType MapAniDBEpisodeType(AniDBEpisodeType episodeType)
        {
            switch (episodeType)
            {
                case AniDBEpisodeType.Episode:
                    return EpisodeType.Normal;
                case AniDBEpisodeType.Special:
                    return EpisodeType.Special;
                case AniDBEpisodeType.Parody:
                    return EpisodeType.Parody;
                case AniDBEpisodeType.Credits:
                    return EpisodeType.ThemeSong;
                case AniDBEpisodeType.Trailer:
                    return EpisodeType.Trailer;
                default:
                case AniDBEpisodeType.Other:
                    return EpisodeType.Unknown;
            }
        }

        public static AniDB GetAniDBInfo(AniDB_Episode ep)
        {
            decimal rating = 0;
            int votes = 0;
            try
            {
                rating = decimal.Parse(ep.Rating);
                votes = int.Parse(ep.Votes);
            }
            catch {}

            var titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.EpisodeID);
            return new AniDB
            {
                ID = ep.EpisodeID,
                Type = MapAniDBEpisodeType((AniDBEpisodeType)ep.EpisodeType),
                EpisodeNumber = ep.EpisodeNumber,
                AirDate = ep.GetAirDateAsDate(),
                Description = ep.Description,
                Rating = new Rating
                {
                    Source = "AniDB",
                    Value = rating,
                    MaxValue = 10,
                    Votes = votes
                },
                Titles = titles.Select(a => new Title
                {
                    Source = "AniDB",
                    Name = a.Title,
                    Language = a.Language.ToLower()
                }).ToList()
            };
        }

        /// <summary>
        /// Get TvDB data from AniDBEpisode ID
        /// </summary>
        /// <param name="id">AniDB Episode ID</param>
        /// <returns></returns>
        public static ActionResult<List<TvDB>> GetTvDBInfo(int id)
        {
            var tvdbEps = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(id);
            return tvdbEps.Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .Select(a =>
                {
                    Rating rating = a.Rating == null ? null : new Rating
                    {
                        Source = "TvDB",
                        Value = a.Rating.Value,
                        MaxValue = 10
                    };
                    return new TvDB
                    {
                        ID = a.Id,
                        Season = a.SeasonNumber,
                        Number = a.EpisodeNumber,
                        AbsoluteNumber = a.AbsoluteNumber ?? 0,
                        Title = a.EpisodeName,
                        Description = a.Overview,
                        AirDate = a.AirDate,
                        Rating = rating,
                        AirsAfterSeason = a.AirsAfterSeason ?? 0,
                        AirsBeforeSeason = a.AirsBeforeSeason ?? 0,
                        AirsBeforeEpisode = a.AirsBeforeEpisode ?? 0,
                        Thumbnail = new Image(a.Id, ImageEntityType.TvDB_Episode, true)
                    };
                }).OrderBy(a => a.Season).ThenBy(a => a.ID).ThenBy(a => a.Number).ToList();
        }

        public static void AddEpisodeVote(HttpContext context, SVR_AnimeEpisode ep, int userID, Vote vote)
        {
            AniDB_Vote dbVote = RepoFactory.AniDB_Vote.GetByEntityAndType(ep.AnimeEpisodeID, AniDBVoteType.Episode);

            if (dbVote == null)
            {
                dbVote = new AniDB_Vote
                {
                    EntityID = ep.AnimeEpisodeID,
                    VoteType = (int) AniDBVoteType.Episode,
                };
            }

            dbVote.VoteValue = (int) Math.Floor(vote.GetRating(1000));

            RepoFactory.AniDB_Vote.Save(dbVote);

            //var cmdVote = new CommandRequest_VoteAnimeEpisode(ep.AniDB_EpisodeID, voteType, vote.GetRating());
            //cmdVote.Save();
        }

        /// <summary>
        /// AniDB specific data for an Episode
        /// </summary>
        public class AniDB
        {
            /// <summary>
            /// AniDB Episode ID
            /// </summary>
            public int ID { get; set; }

            /// <summary>
            /// Episode Type
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            public EpisodeType Type { get; set; }

            /// <summary>
            /// Episode Number
            /// </summary>
            public int EpisodeNumber { get; set; }

            /// <summary>
            /// First Listed Air Date. This may not be when it aired, but an early release date
            /// </summary>
            public DateTime? AirDate { get; set; }

            /// <summary>
            /// Titles for the Episode
            /// </summary>
            public List<Title> Titles { get; set; }

            /// <summary>
            /// AniDB Episode Summary
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Episode Rating
            /// </summary>
            public Rating Rating { get; set; }
        }

        public class TvDB
        {
            /// <summary>
            /// TvDB Episode ID
            /// </summary>
            public int ID { get; set; }

            /// <summary>
            /// Season Number, 0 is Specials. TvDB's Season system doesn't always make sense for anime, so don't count on it
            /// </summary>
            public int Season { get; set; }

            /// <summary>
            /// Episode Number in the Season. This is not Absolute Number
            /// </summary>
            public int Number { get; set; }

            /// <summary>
            /// Absolute Episode Number. Keep in mind that due to reordering, this may not be accurate.
            /// </summary>
            public int AbsoluteNumber { get; set; }

            /// <summary>
            /// Episode Title, in the language selected for TvDB. TvDB doesn't allow pulling more than one language at a time, so this isn't a list.
            /// </summary>
            public string Title { get; set; }

            /// <summary>
            /// Episode Description, in the language selected for TvDB. See Title for more info on Language.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Air Date. Unfortunately, the TvDB air date doesn't necessarily conform to a specific timezone, so it can be a day off. If you see one that's wrong, please fix it on TvDB. You have the ID here in this model for easy lookup.
            /// </summary>
            public DateTime? AirDate { get; set; }

            /// <summary>
            /// Mostly for specials. It shows when in the timeline the episode aired. I wouldn't count on it, as it's often blank.
            /// </summary>
            public int AirsAfterSeason { get; set; }

            /// <summary>
            /// Mostly for specials. It shows when in the timeline the episode aired. I wouldn't count on it, as it's often blank.
            /// </summary>
            public int AirsBeforeSeason { get; set; }

            /// <summary>
            /// Like AirsAfterSeason, it is for determining where in the timeline an episode airs. Also often blank.
            /// </summary>
            public int AirsBeforeEpisode { get; set; }

            /// <summary>
            /// Rating of the episode
            /// </summary>
            public Rating Rating { get; set; }

            /// <summary>
            /// The TvDB Thumbnail. Later, we'll have more thumbnail support, and episodes will have an Images endpoint like series, but for now, this will do.
            /// </summary>
            public Image Thumbnail { get; set; }
        }

        public class EpisodeIDs : IDs
        {
            #region XRefs

            // These are useful for many things, but for clients, it is mostly auxiliary

            /// <summary>
            /// The AniDB ID
            /// </summary>
            [Required]
            public int AniDB { get; set; }

            /// <summary>
            /// The TvDB IDs
            /// </summary>
            public List<int> TvDB { get; set; } = new List<int>();

            // TODO Support for TvDB string IDs (like in the new URLs) one day maybe
            #endregion
        }
    }

    public enum EpisodeType
    {
        /// <summary>
        /// The episode type is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
        /// </summary>
        Other = 1,

        /// <summary>
        /// A normal episode.
        /// </summary>
        Normal = 2,

        /// <summary>
        /// A special episode.
        /// </summary>
        Special = 3,

        /// <summary>
        /// A trailer.
        /// </summary>
        Trailer = 4,

        /// <summary>
        /// Either an opening-song, or an ending-song.
        /// </summary>
        ThemeSong = 5,

        /// <summary>
        /// Intro, and/or opening-song.
        /// </summary>
        OpeningSong = 6,

        /// <summary>
        /// Outro, end-roll, credits, and/or ending-song.
        /// </summary>
        EndingSong = 7,

        /// <summary>
        /// AniDB parody type. Where else would this be useful?
        /// </summary>
        Parody = 8,

        /// <summary>
        /// A interview tied to the series.
        /// </summary>
        Interview = 9,

        /// <summary>
        /// A DVD or BD extra, e.g. BD-menu or deleted scenes.
        /// </summary>
        Extra = 10,
    }
}
