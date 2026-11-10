//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/GTPS2ModelTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

namespace GTPS2ModelTool.Core.Config.Callbacks;

public class TailLampCallback
{
    /// <summary>
    /// Shapes when off.
    /// </summary>
    public List<string> Off { get; set; } = [];

    /// <summary>
    /// Shapes when on.
    /// </summary>
    public List<string> On { get; set; } = [];

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;
            foreach (var elem in Off)
                hash += elem.GetHashCode();

            foreach (var elem in On)
                hash += elem.GetHashCode();

            return hash;
        }
    }
}
