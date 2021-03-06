// DynamicOpenVR - Unity scripts to allow dynamic creation of OpenVR actions at runtime.
// Copyright � 2019 Nicolas Gnyra

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see http://www.gnu.org/licenses/.

using UnityEngine;
using Valve.VR;

namespace DynamicOpenVR.IO
{
	public class BooleanInput : Input
    {
        private int _lastFrame;
        private InputDigitalActionData_t _actionData;

        private InputDigitalActionData_t actionData
        {
            get
            {
                if (_lastFrame != Time.frameCount)
                {
                    _actionData = OpenVRWrapper.GetDigitalActionData(handle);
                }

                _lastFrame = Time.frameCount;

                return _actionData;
            }
        }

		public BooleanInput(string name) : base(name) { }

        /// <summary>
        /// Is set to True if this action is bound to an input source that is present in the system and is in an action set that is active.
        /// </summary>
        public override bool IsActive()
        {
            return actionData.bActive;
        }

        /// <summary>
        /// The current state of this digital action. True means the user wants to perform this action.
        /// </summary>
		public bool GetState()
		{
            return actionData.bState;
		}

        /// <summary>
        /// If the state changed from disabled to enabled since it was last checked.
        /// </summary>
		public bool GetActiveChange()
		{
			return actionData.bState && actionData.bChanged;
		}

        /// <summary>
        /// If the state changed from enabled to disabled since it was last checked.
        /// </summary>
        public bool GetInactiveChange()
        {
			return !actionData.bState && actionData.bChanged;
		}
	}
}
