using System;
using System.Collections.Generic;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.TraktTV
{
    public class TraktSummaryContainer
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int AnimeID { get; set; }

        // Trakt ID
        public Dictionary<string, TraktDetailsContainer> TraktDetails = new Dictionary<string, TraktDetailsContainer>();

        // All the Trakt cross refs for this anime
        private List<CrossRef_AniDB> crossRefTraktV2;

        public List<CrossRef_AniDB> CrossRefTraktV2
        {
            get
            {
                if (crossRefTraktV2 == null)
                {
                    PopulateCrossRefs();
                }
                return crossRefTraktV2;
            }
        }

        private void PopulateCrossRefs()
        {
            try
            {
                crossRefTraktV2 = RepoFactory.CrossRef_AniDB.GetByAniDB(AnimeID, Shoko.Models.Constants.Providers.Trakt);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        // All the episodes regardless of which cross ref they come from 
        private Dictionary<int, Trakt_Episode> dictTraktEpisodes;

        public Dictionary<int, Trakt_Episode> DictTraktEpisodes
        {
            get
            {
                if (dictTraktEpisodes == null)
                {
                    PopulateDictTraktEpisodes();
                }
                return dictTraktEpisodes;
            }
        }

        private void PopulateDictTraktEpisodes()
        {
            try
            {
                dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
                foreach (TraktDetailsContainer det in TraktDetails.Values)
                {
                    if (det != null)
                    {
                        // create a dictionary of absolute episode numbers for Trakt episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        List<Trakt_Episode> eps = det.TraktEpisodes;

                        if (eps != null)
                        {
                            int i = 1;
                            foreach (Trakt_Episode ep in eps)
                            {
                                // ignore episode 0, this can't be mapped to Trakt
                                if (ep.EpisodeNumber > 0)
                                {
                                    dictTraktEpisodes[i] = ep;
                                    i++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }


        public void Populate(int animeID)
        {
            AnimeID = animeID;

            try
            {
                PopulateCrossRefs();
                PopulateTraktDetails();
                PopulateDictTraktEpisodes();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        private void PopulateTraktDetails()
        {
            if (CrossRefTraktV2 == null) return;

            foreach (CrossRef_AniDB xref in CrossRefTraktV2)
            {
                TraktDetailsContainer det = new TraktDetailsContainer(xref.ProviderID);
                TraktDetails[xref.ProviderID] = det;
            }
        }
    }
}