using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccSaber.UI.Leaderboard;
using AccSaber.UI.Panel;
using HMUI;
using AccSaber.Downloaders;
using LeaderboardCore.Managers;
using LeaderboardCore.Models;
using Zenject;
using AccSaber.Data;
using AccSaber.Interfaces;
using AccSaber.Models;
using LeaderboardCore.Interfaces;
using SiraUtil.Logging;
using static StandardLevelDetailViewController.ContentType;

namespace AccSaber.Managers
{
    class AccSaberManager : CustomLeaderboard, IInitializable, IDisposable, INotifyLeaderboardSet
    {
        private readonly CustomLeaderboardManager _customLeaderboardManager;

        private readonly AccSaberPanelViewController _accSaberPanelController;
        protected override ViewController panelViewController => _accSaberPanelController;
        protected override ViewController leaderboardViewController => _accSaberLeaderboardViewController;

        private readonly LevelCollectionNavigationController _navigationController;

        private AccSaberDownloader _accSaberDownloader;
        private readonly AccSaberData _accSaberData;
        private readonly List<AccSaberAPISong> _accSaberAPISong;
        private List<AccSaberLeaderboardEntry> _leaderboardEntries;
        private List<AccSaberUserModel> _accSaberUserModels;

        

        private readonly AccSaberLeaderboardViewController _accSaberLeaderboardViewController;

        private List<INotifyViewActivated> _notifyViewActivateds;

        private List<ILeaderboardEntriesUpdater> _leaderboardEntriesUpdaters;
        private List<IDifficultyBeatmapUpdater> difficultyBeatmapUpdaters;

        private LeaderboardDownloader _leaderboardDownloader;

        private readonly SiraLog _log;
        private CancellationTokenSource levelInfoTokenSource;
        private CancellationTokenSource leaderboardTokenSource;
        private readonly CancellationTokenSource _cancellationToken;

        private int pageNumber;

        public AccSaberManager(
            AccSaberPanelViewController accSaberPanelController,
            CustomLeaderboardManager customLeaderboardManager,
            SiraLog log,
            LevelCollectionNavigationController navigationController,
            AccSaberData accSaberData,
            AccSaberDownloader accSaberDownloader,
            List<AccSaberAPISong> accSaberAPISong,
            List<AccSaberLeaderboardEntry> leaderboardEntries,
            List<INotifyViewActivated> notifyViewActivateds,
            AccSaberLeaderboardViewController accSaberLeaderboardViewController,
            List<AccSaberUserModel> accSaberUserModels, 
            LeaderboardDownloader leaderboardDownloader)
        {
            _customLeaderboardManager = customLeaderboardManager;
            _log = log;
            _accSaberPanelController = accSaberPanelController;
            _navigationController = navigationController;
            _accSaberData = accSaberData;
            _accSaberDownloader = accSaberDownloader;
            _accSaberAPISong = accSaberAPISong;
            _leaderboardEntries = leaderboardEntries;
            _notifyViewActivateds = notifyViewActivateds;
            _accSaberLeaderboardViewController = accSaberLeaderboardViewController;
            _accSaberUserModels = accSaberUserModels;
            _leaderboardDownloader = leaderboardDownloader;
        }


        public void Initialize()
        {
            _accSaberLeaderboardViewController.didActivateEvent += OnViewActivated;
            _accSaberLeaderboardViewController.PageRequested += OnPageRequested;
            _navigationController.didChangeLevelDetailContentEvent += OnSongChange;
            _navigationController.didChangeDifficultyBeatmapEvent += OnBeatmapChange;

            difficultyBeatmapUpdaters = new List<IDifficultyBeatmapUpdater> { _accSaberLeaderboardViewController };
            _leaderboardEntriesUpdaters = new List<ILeaderboardEntriesUpdater> { _accSaberLeaderboardViewController };
        }

        public void Dispose()
        {
            _accSaberLeaderboardViewController.didActivateEvent -= OnViewActivated;
            _accSaberLeaderboardViewController.PageRequested -= OnPageRequested;
            _navigationController.didChangeLevelDetailContentEvent -= OnSongChange;
            _navigationController.didChangeDifficultyBeatmapEvent -= OnBeatmapChange;
        }


        private void OnBeatmapChange(LevelCollectionNavigationController collectionNavigationController,
            IDifficultyBeatmap difficultyBeatmap)
        {
            HandleRankedSongLeaderboard(_navigationController);
            HandleCategoryColorChange();
        }

        private void OnSongChange(LevelCollectionNavigationController collectionNavigationController,
            StandardLevelDetailViewController.ContentType contentType)
        {
            if (contentType == OwnedAndReady)
            {
                HandleRankedSongLeaderboard(collectionNavigationController);
                HandleCategoryColorChange();
            }
        }

        private void HandleCategoryColorChange()
        {
            _accSaberPanelController.RefreshAndSkewBannerColor();
        }


        private void HandleRankedSongLeaderboard(LevelCollectionNavigationController collectionNavigationController)
        {
            var difficultyBeatmap = collectionNavigationController.selectedDifficultyBeatmap;

            if (difficultyBeatmap == null) return;

            if (!_accSaberData.IsMapDataInitialized)
            {
                return;
            }

            if (_accSaberData.GetMapsFromHash(difficultyBeatmap.level.levelID.GetRankedSongHash()).Any(x =>
                    x.difficulty.Equals(difficultyBeatmap.difficulty.ToString(), StringComparison.InvariantCultureIgnoreCase)))
            {
                _customLeaderboardManager.Register(this);
            }
            else
            {
                _customLeaderboardManager.Unregister(this);
            }
        }

        public void OnLeaderboardSet(IDifficultyBeatmap difficultyBeatmap) => 
            _ = OnLeaderboardSetAsync(difficultyBeatmap);

        private async Task OnLeaderboardSetAsync(IDifficultyBeatmap difficultyBeatmap)
        {
            levelInfoTokenSource?.Cancel();
            levelInfoTokenSource?.Dispose();
            List<AccSaberLeaderboardEntry> accSaberLeaderboardEntries = null;

            if (difficultyBeatmap.level is CustomPreviewBeatmapLevel)
            {
                levelInfoTokenSource = new CancellationTokenSource();
                accSaberLeaderboardEntries = await _leaderboardDownloader.GetLevelInfoAsync(difficultyBeatmap, levelInfoTokenSource.Token);
                _log.Debug("This is past the request.");
            }

            foreach (var updater in difficultyBeatmapUpdaters)
            {
                await IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                    updater.DifficultyBeatmapUpdated(difficultyBeatmap, accSaberLeaderboardEntries));
            }
        }

        private void OnPageRequested(IDifficultyBeatmap difficultyBeatmap, ILeaderboardSource leaderboardSource, int page) =>
            _ = OnPageRequestedAsync(difficultyBeatmap, leaderboardSource, page);
        
        private async Task OnPageRequestedAsync(IDifficultyBeatmap difficultyBeatmap, ILeaderboardSource leaderboardSource, int page)
        {
            leaderboardTokenSource?.Cancel();
            leaderboardTokenSource?.Dispose();
            leaderboardTokenSource = new CancellationTokenSource();
            var leaderboardEntries = await leaderboardSource.GetScoresAsync(difficultyBeatmap, page, leaderboardTokenSource.Token);

            if (leaderboardEntries is null)
            {
                return;
            }
            _log.Debug(leaderboardEntries);

            foreach (var updater in _leaderboardEntriesUpdaters)
            {
                await IPA.Utilities.Async.UnityMainThreadTaskScheduler.Factory.StartNew(() => 
                    updater.LeaderboardEntriesUpdated(leaderboardEntries));
            }
        }
        
        private void OnViewActivated(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            foreach (var notifyViewActivated in _notifyViewActivateds)
            {
                notifyViewActivated.ViewActivated(_accSaberLeaderboardViewController, firstActivation, addedToHierarchy, screenSystemEnabling);
            }
        }
    }
}