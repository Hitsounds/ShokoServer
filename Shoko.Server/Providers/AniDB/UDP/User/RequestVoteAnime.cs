using System;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    /// <summary>
    /// Vote for an anime
    /// </summary>
    public class RequestVoteAnime : UDPBaseRequest<ResponseVote>
    {
        /// <summary>
        /// AnimeID to vote on
        /// </summary>
        public int AnimeID { get; set; }

        /// <summary>
        /// Between 0 exclusive and 10 inclusive, will be rounded to nearest tenth
        /// </summary>
        public double Value { get; set; }

        private int AniDBValue => (int) (Math.Round(Value, 1, MidpointRounding.AwayFromZero) * 100D);

        /// <summary>
        /// If the anime is not finished (or you haven't finished it), then it is Temporary
        /// </summary>
        public bool Temporary { get; set; }

        protected override string BaseCommand => $"VOTE type={(Temporary ? 2 : 1)}&aid={AnimeID}&value={AniDBValue}";

        protected override UDPBaseResponse<ResponseVote> ParseResponse(UDPReturnCode code, string receivedData)
        {
            string[] parts = receivedData.Split('|');
            if (parts.Length != 4) throw new UnexpectedAniDBResponseException("Incorrect Number of Parts Returned", code, receivedData);
            if (!int.TryParse(parts[1], out int value)) throw new UnexpectedAniDBResponseException("Value should be an int, but it's not", code, receivedData);
            if (!int.TryParse(parts[2], out int type)) throw new UnexpectedAniDBResponseException("Vote type should be an int, but it's not", code, receivedData);
            if (!int.TryParse(parts[3], out int id)) throw new UnexpectedAniDBResponseException("ID should be an int, but it's not", code, receivedData);

            return new UDPBaseResponse<ResponseVote> {Code = code, Response = new ResponseVote
            {
                EntityName = parts[0],
                Value = value,
                Type = (VoteType) type,
                EntityID = id
            }};
        }
    }
}
