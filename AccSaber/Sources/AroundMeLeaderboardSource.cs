using AccSaber.Utils;
using SiraUtil.Logging;
using SiraUtil.Web;
using System.Threading.Tasks;
using UnityEngine;

namespace AccSaber.Sources
{
    // Probably can be simplified
    public class AroundMeLeaderboardSource : GlobalLeaderboardSource
    {
        public string HoverHint => "Around Me";
        public Sprite _icon;
        private readonly UserIDUtils _userIDUtils;

        public AroundMeLeaderboardSource(IHttpService httpService, SiraLog siraLog, UserIDUtils userIDUtils) : base(httpService, siraLog)
        {
            _userIDUtils = userIDUtils;
        }

        override public Sprite Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = BeatSaberMarkupLanguage.Utilities.FindSpriteInAssembly("AccSaber.Resources.PlayerIcon.png");
                }
                return _icon;
            }
        }

        override public bool Scrollable => false;

        
        override public async Task<string> UrlPostFix() { 
            return "/" + Constants.AROUND_ME + (await _userIDUtils.GetUserInfo()).platformUserId;
        }
    }
}