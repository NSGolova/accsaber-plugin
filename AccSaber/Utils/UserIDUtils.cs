using System.Threading;
using System.Threading.Tasks;
using SiraUtil.Logging;
using Zenject;

namespace AccSaber.Utils
{
    public class UserIDUtils : IInitializable
    {
        private readonly IPlatformUserModel _userModel = null!;

        public UserInfo UserInfo;

        public UserIDUtils(IPlatformUserModel userModel)
        {
            _userModel = userModel;
        }

        public void Initialize()
        {
            _ = GetUserInfo();
        }

        public async Task<UserInfo> GetUserInfo()
        {
            if (UserInfo == null) {
                UserInfo = await _userModel.GetUserInfo();
            }

            return UserInfo;
        }
    }
}