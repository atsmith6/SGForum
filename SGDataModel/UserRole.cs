using System;

namespace SGDataModel
{
    public struct UserRole
    {
		public const int Admin = 3;
		public const int User = 2;
		public const int Guest = 1;

		private int _raw;

		public static bool RoleIsValid(int role)
		{
			return role >= Guest && role <= Admin;
		}

        public bool IsAdmin
		{
			get
			{
				return _raw >= Admin;
			}
		}
		public bool IsUser 
		{ 	
			get
			{
				return _raw >= User;
			}
		}

		public bool IsGuest 
		{
			get
			{
				return _raw >= Guest;
			}
		}

		public UserRole(int rawRole)
		{
			
			_raw = rawRole;
		}

		public int ToInt()
		{
			return _raw;
		}
	}
}