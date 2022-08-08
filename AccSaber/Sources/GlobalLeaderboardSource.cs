using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccSaber.Downloaders;
using AccSaber.Interfaces;
using AccSaber.Models;
using AccSaber.Utils;
using SiraUtil.Logging;
using SiraUtil.Web;
using UnityEngine;

namespace AccSaber.Sources
{
    public class GlobalLeaderboardSource : ILeaderboardSource
    {
        private readonly IHttpService _httpService;
        private readonly List<List<AccSaberLeaderboardEntry>> leaderboardCache = new();
        private readonly SiraLog _siraLog;

        public GlobalLeaderboardSource(IHttpService httpService, SiraLog siraLog)
        {
            _httpService = httpService;
            _siraLog = siraLog;
        }

        public virtual async Task<string> UrlPostFix() {
            return "";
        }

        public virtual string HoverHint => "Global";
        private Sprite _icon;
        public virtual Sprite Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("AccSaber.Resources.GlobalIcon.png");
                }
                return _icon;
            }
        }

        public async Task<List<AccSaberLeaderboardEntry>> GetScoresAsync(IDifficultyBeatmap difficultyBeatmap,
            int page = 0, CancellationToken? cancellationToken = null)
        {
            if (leaderboardCache.Count < page + 1)
            {
                _siraLog.Debug("knob");
                var beatmapString = GameUtils.DifficultyBeatmapToString(difficultyBeatmap);
                _siraLog.Debug("knob2");
                if (beatmapString == null)
                {
                    _siraLog.Debug("beatmap is null");
                    return null;
                }
                
                try
                {
                    string url =  Constants.API_URL + Constants.LEADERBOARDS_ENDPOINT + beatmapString + await UrlPostFix() +
                                                               Constants.PAGINATION_PAGE + page + Constants.PAGINATION_PAGESIZE + 10;
                    _siraLog.Debug(url);
                    var response = await _httpService.GetAsync(url, cancellationToken: cancellationToken ?? CancellationToken.None);
                    _siraLog.Debug("sent request, going to parse.");
                    var scores = await ResponseParser.ParseWebResponse<List<AccSaberLeaderboardEntry>>(response);
                    if (scores != null)
                    {
                        _siraLog.Debug($"Adding scores from {scores} with count {scores.Count}");
                        leaderboardCache.Add(scores);
                    }
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
            }
            return page < leaderboardCache.Count ? leaderboardCache[page] : null;
        }

        public virtual bool Scrollable => true;
        public void ClearCache() => leaderboardCache.Clear();
    }
}