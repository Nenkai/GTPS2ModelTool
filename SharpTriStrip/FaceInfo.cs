using System;

namespace SharpTriStrip
{
	/// <summary>
	/// Represents a single face/triangle consisting of three indices. Used internally only.
	/// </summary>
	public class FaceInfo : IEquatable<FaceInfo>
	{
		/// <summary>
		/// First index of this <see cref="FaceInfo"/>.
		/// </summary>
		public int V0 { get; set; }

		/// <summary>
		/// Second index of this <see cref="FaceInfo"/>.
		/// </summary>
		public int V1 { get; set; }

		/// <summary>
		/// Third index of this <see cref="FaceInfo"/>.
		/// </summary>
		public int V2 { get; set; }

		/// <summary>
		/// <see cref="StripInfo"/> to which this <see cref="FaceInfo"/> belongs to.
		/// </summary>
		public int StripID { get; set; }

		/// <summary>
		/// Experimental <see cref="StripInfo"/> to which this <see cref="FaceInfo"/> belongs to.
		/// </summary>
		public int TestStripID { get; set; }

		/// <summary>
		/// Unique experimental identifier of this <see cref="FaceInfo"/>, used only in experiments.
		/// </summary>
		public int ExperimentID { get; set; }

		/// <summary>
		/// <see langword="true"/> if this <see cref="FaceInfo"/> is fake and does not exist in the initial 
		/// index buffer given; otherwise, <see langword="false"/>.
		/// </summary>
		public bool IsFake { get; set; }

		/// <summary>
		/// Creates new instance of <see cref="FaceInfo"/> with indices given.
		/// </summary>
		/// <param name="v0">First index of this <see cref="FaceInfo"/>.</param>
		/// <param name="v1">Second index of this <see cref="FaceInfo"/>.</param>
		/// <param name="v2">Third index of this <see cref="FaceInfo"/>.</param>
		/// <param name="isFake"><see langword="true"/> if this <see cref="FaceInfo"/> is fake and does not exist 
		/// in the initial index buffer given; otherwise, <see langword="false"/>.</param>
		public FaceInfo(int v0, int v1, int v2, bool isFake = false)
		{
			this.V0 = v0;
			this.V1 = v1;
			this.V2 = v2;
			this.StripID = -1;
			this.TestStripID = -1;
			this.ExperimentID = -1;
			this.IsFake = isFake;
		}

		/// <summary>
		/// Indicates whether the current <see cref="FaceInfo"/> is equal to another <see cref="FaceInfo"/> specified.
		/// </summary>
		/// <param name="other">A <see cref="FaceInfo"/> to compare with this <see cref="FaceInfo"/>.</param>
		/// <returns><see langword="true"/> if the current <see cref="FaceInfo"/> is equal to the other 
		/// <see cref="FaceInfo"/> specified; otherwise, <see langword="false"/>.</returns>
		public bool Equals(FaceInfo other)
		{
			if (other is null)
			{
				return false;
			}

			bool result = true;

			result &= this.V0 == other.V0;
			result &= this.V1 == other.V1;
			result &= this.V2 == other.V2;
			result &= this.StripID == other.StripID;
			result &= this.TestStripID == other.TestStripID;
			result &= this.ExperimentID == other.ExperimentID;
			result &= this.IsFake == other.IsFake;

			return result;
		}

		/// <summary>
		/// Determines whether the specified object is equal to the current <see cref="FaceInfo"/>.
		/// </summary>
		/// <param name="obj">The object to compare with the current <see cref="FaceInfo"/>.</param>
		/// <returns><see langword="true"/> if the specified object is equal to the current <see cref="FaceInfo"/>;
		/// otherwise, <see langword="false"/>.</returns>
		public override bool Equals(object obj)
		{
			return obj is FaceInfo faceInfo && this.Equals(faceInfo);
		}

		/// <summary>
		/// Serves as the default hash function.
		/// </summary>
		/// <returns>A hash code for the current <see cref="FaceInfo"/>.</returns>
		public override int GetHashCode()
		{
			return Tuple.Create(this.V0, this.V1, this.V2, this.StripID, this.TestStripID, this.ExperimentID, this.IsFake).GetHashCode();
		}

		/// <summary>
		/// Returns a string that represents the current <see cref="FaceInfo"/>.
		/// </summary>
		/// <returns>A string that represents the current <see cref="FaceInfo"/>.</returns>
		public override string ToString()
		{
			return $"<{this.V0}, {this.V1}, {this.V2}> -> {this.StripID}";
		}

		/// <summary>
		/// Compares two <see cref="FaceInfo"/> instances and checks whether they are equal.
		/// </summary>
		/// <param name="lhs">Left-hand-side <see cref="FaceInfo"/> to compare.</param>
		/// <param name="rhs">Right-hand-side <see cref="FaceInfo"/> to compare.</param>
		/// <returns><see langword="true"/> if two instances are equal; otherwise, <see langword="false"/>.</returns>
		public static bool operator ==(FaceInfo lhs, FaceInfo rhs) => lhs is null ? rhs is null : lhs.Equals(rhs);

		/// <summary>
		/// Compares two <see cref="FaceInfo"/> instances and checks whether they are not equal.
		/// </summary>
		/// <param name="lhs">Left-hand-side <see cref="FaceInfo"/> to compare.</param>
		/// <param name="rhs">Right-hand-side <see cref="FaceInfo"/> to compare.</param>
		/// <returns><see langword="true"/> if two instances are not equal; otherwise, <see langword="false"/>.</returns>
		public static bool operator !=(FaceInfo lhs, FaceInfo rhs) => !(lhs == rhs);
	}
}
