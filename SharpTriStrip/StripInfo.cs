using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpTriStrip
{
	/// <summary>
	/// Represents a single strip that start face information, <see cref="FaceInfo"/> information, and
	/// (optional) unique identifier. Used internally only.
	/// </summary>
	public class StripInfo
	{
		/// <summary>
		/// <see cref="StripStartInfo"/> of this <see cref="StripInfo"/> with start information about faces and edges.
		/// </summary>
		public StripStartInfo StartInfo { get; }

		/// <summary>
		/// <see cref="List{T}"/> of type <see cref="FaceInfo"/> that contains all faces in this <see cref="StripInfo"/>.
		/// </summary>
		public List<FaceInfo> FaceInfos { get; }

		/// <summary>
		/// Unique identifier of this <see cref="StripInfo"/> that is used to group <see cref="FaceInfo"/>.
		/// </summary>
		public int StripID { get; set; }

		/// <summary>
		/// Experimental identifier of this <see cref="StripInfo"/> that is used in making strip tests.
		/// </summary>
		public int ExperimentID { get; set; }

		/// <summary>
		/// <see langword="true"/> if this <see cref="StripInfo"/> has already been finialized; otherwise, <see langword="false"/>.
		/// </summary>
		public bool Visited { get; set; }

		/// <summary>
		/// Number of degenerate <see cref="FaceInfo"/> that this <see cref="StripInfo"/> contains. To check whether
		/// a <see cref="FaceInfo"/> is degenerate, refer to <see cref="Stripifier.IsDegenerate(FaceInfo)"/>.
		/// </summary>
		public int NumDegenerates { get; set; }

		/// <summary>
		/// <see langword="true"/> if this <see cref="StripInfo"/> is experimental, meaning has 
		/// <see cref="ExperimentID"/> >= 0; otherwise, <see langword="false"/>.
		/// </summary>
		public bool IsExperiment => this.ExperimentID >= 0;

		/// <summary>
		/// Creates a new instance of <see cref="StripInfo"/>.
		/// </summary>
		/// <param name="startInfo"><see cref="StripStartInfo"/> that contains start face of this <see cref="StripInfo"/>.</param>
		/// <param name="stripID">Unique identifier for this <see cref="StripInfo"/>.</param>
		/// <param name="experimentID">Experimental identifier for this <see cref="StripInfo"/>. Used in experiments only.</param>
		public StripInfo(StripStartInfo startInfo, int stripID, int experimentID = -1)
		{
			this.StartInfo = startInfo;
			this.StripID = stripID;
			this.ExperimentID = experimentID;
			this.FaceInfos = new List<FaceInfo>();
		}

		private static int GetNextIndex(List<ushort> indices, FaceInfo face, Action<string> debugWriter = null)
		{
			var debugNull = debugWriter is null;
			int numIndices = indices.Count;
			Debug.Assert(numIndices >= 2);

			int v0 = indices[numIndices - 2];
			int v1 = indices[numIndices - 1];

			int fv0 = face.V0;
			int fv1 = face.V1;
			int fv2 = face.V2;

			if (fv0 != v0 && fv0 != v1)
			{
				if ((fv1 != v0 && fv1 != v1) || (fv2 != v0 && fv2 != v1))
				{
					if (!debugNull)
					{
						debugWriter.Invoke("GetNextIndex: Triangle doesn't have all of its vertices");
						debugWriter.Invoke("GetNextIndex: Duplicate triangle probably got us derailed");
					}
				}

				return fv0;
			}

			if (fv1 != v0 && fv1 != v1)
			{
				if ((fv0 != v0 && fv0 != v1) || (fv2 != v0 && fv2 != v1))
				{
					if (!debugNull)
					{
						debugWriter.Invoke("GetNextIndex: Triangle doesn't have all of its vertices");
						debugWriter.Invoke("GetNextIndex: Duplicate triangle probably got us derailed");
					}
				}

				return fv1;
			}

			if (fv2 != v0 && fv2 != v1)
			{
				if ((fv0 != v0 && fv0 != v1) || (fv1 != v0 && fv1 != v1))
				{
					if (!debugNull)
					{
						debugWriter.Invoke("GetNextIndex: Triangle doesn't have all of its vertices");
						debugWriter.Invoke("GetNextIndex: Duplicate triangle probably got us derailed");
					}
				}

				return fv2;
			}

			// shouldn't get here, but let's try and fail gracefully
			if ((fv0 == fv1) || (fv0 == fv2))
			{
				return fv0;
			}
			else if ((fv1 == fv0) || (fv1 == fv2))
			{
				return fv1;
			}
			else if ((fv2 == fv0) || (fv2 == fv1))
			{
				return fv2;
			}
			else
			{
				return -1;
			}
		}

		private bool Unique(List<FaceInfo> faceInfos, FaceInfo faceInfo)
		{
			// bools to indicate whether a vertex is in the faceVec or not
			bool bv0 = false;
			bool bv1 = false;
			bool bv2 = false;

			for (int i = 0; i < faceInfos.Count; ++i)
			{
				if (!bv0)
				{
					if ((faceInfos[i].V0 == faceInfo.V0) ||
						(faceInfos[i].V1 == faceInfo.V0) ||
						(faceInfos[i].V2 == faceInfo.V0))
					{
						bv0 = true;
					}
				}

				if (!bv1)
				{
					if ((faceInfos[i].V0 == faceInfo.V1) ||
						(faceInfos[i].V1 == faceInfo.V1) ||
						(faceInfos[i].V2 == faceInfo.V1))
					{
						bv1 = true;
					}
				}

				if (!bv2)
				{
					if ((faceInfos[i].V0 == faceInfo.V2) ||
						(faceInfos[i].V1 == faceInfo.V2) ||
						(faceInfos[i].V2 == faceInfo.V2))
					{
						bv2 = true;
					}
				}

				// the face is not unique, all it's vertices exist in the face vector
				if (bv0 && bv1 && bv2)
				{
					return false;
				}
			}

			// if we get out here, it's unique
			return true;
		}

		private void Combine(List<FaceInfo> forward, List<FaceInfo> backward)
		{
			// add backward faces
			int numFaces = backward.Count;

			for (int i = numFaces - 1; i >= 0; i--)
			{
				this.FaceInfos.Add(backward[i]);
			}

			// add forward faces
			this.FaceInfos.AddRange(forward);
		}

		/// <summary>
		/// Checks whether the <see cref="FaceInfo"/> provided belongs to this <see cref="StripInfo"/>.
		/// </summary>
		/// <param name="faceInfo"><see cref="FaceInfo"/> instance to check.</param>
		/// <returns><see langword="true"/> if <see cref="FaceInfo"/> is not <see langword="null"/> and if its identifier
		/// matches identifier <see cref="StripID"/> of this strip; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsInStrip(FaceInfo faceInfo)
		{
			if (faceInfo is null)
			{
				return false;
			}

			return this.ExperimentID >= 0
				? faceInfo.TestStripID == this.StripID
				: faceInfo.StripID == this.StripID;
		}

		/// <summary>
		/// Checks if either the <see cref="FaceInfo"/> has a real strip index because it is already assigned to a
		/// committed strip OR it is assigned in an experiment and the experiment index is the one we are building for.
		/// </summary>
		/// <param name="faceInfo"><see cref="FaceInfo"/> to check.</param>
		/// <returns><see langword="true"/> if <see cref="FaceInfo.StripID"/> is >= 0 or if its experimental
		/// identifier <see cref="FaceInfo.ExperimentID"/> equals <see cref="ExperimentID"/> of this strip;
		/// otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsMarked(FaceInfo faceInfo)
		{
			return (faceInfo.StripID >= 0) || (this.IsExperiment && faceInfo.ExperimentID == this.ExperimentID);
		}

		/// <summary>
		/// Marks the face with the current <see cref="StripID"/> of this <see cref="StripInfo"/>.
		/// </summary>
		/// <param name="faceInfo"><see cref="FaceInfo"/> to mark.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void MarkTriangle(FaceInfo faceInfo)
		{
			Debug.Assert(!this.IsMarked(faceInfo));

			if (this.IsExperiment)
			{
				faceInfo.ExperimentID = this.ExperimentID;
				faceInfo.TestStripID = this.StripID;
			}
			else
			{
				Debug.Assert(faceInfo.StripID == -1);
				faceInfo.ExperimentID = -1;
				faceInfo.StripID = this.StripID;
			}
		}

		/// <summary>
		/// Checks if the input <see cref="FaceInfo"/> and this <see cref="StripInfo"/> share an edge.
		/// </summary>
		/// <param name="faceInfo"><see cref="FaceInfo"/> to check.</param>
		/// <param name="edgeInfos"><see cref="List{T}"/> of all <see cref="EdgeInfo"/> instances that
		/// provide edge information of all faces, including this <see cref="StripInfo"/>.</param>
		/// <returns><see langword="true"/> if <see cref="FaceInfo"/> provided shares at least one edge
		/// with this <see cref="StripInfo"/>; otherwise, <see langword="false"/>.</returns>
		public bool SharesEdge(FaceInfo faceInfo, List<EdgeInfo> edgeInfos)
		{
			// check v0->v1 edge
			var currEdge = Stripifier.FindEdgeInfo(edgeInfos, faceInfo.V0, faceInfo.V1) as EdgeInfo;

			if (this.IsInStrip(currEdge.Face0) || this.IsInStrip(currEdge.Face1))
			{
				return true;
			}

			// check v1->v2 edge
			currEdge = Stripifier.FindEdgeInfo(edgeInfos, faceInfo.V1, faceInfo.V2);

			if (this.IsInStrip(currEdge.Face0) || this.IsInStrip(currEdge.Face1))
			{
				return true;
			}

			// check v2->v0 edge
			currEdge = Stripifier.FindEdgeInfo(edgeInfos, faceInfo.V2, faceInfo.V0);

			if (this.IsInStrip(currEdge.Face0) || this.IsInStrip(currEdge.Face1))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Builds a strip forward as far as we can go, then builds backwards, and joins the unified list 
		/// of <see cref="FaceInfo"/>.
		/// </summary>
		/// <param name="edgeInfos"><see cref="List{T}"/> of all <see cref="EdgeInfo"/> that provide edge information
		/// for all faces and strips, including this one.</param>
		public void Build(List<EdgeInfo> edgeInfos)
		{
			// used in building the strips forward and backward
			var scratchIndices = new List<ushort>();

			// build forward... start with the initial face
			var forwardFaces = new List<FaceInfo>();
			var backwardFaces = new List<FaceInfo>();

			forwardFaces.Add(this.StartInfo.Face);

			this.MarkTriangle(this.StartInfo.Face);

			int v0 = this.StartInfo.ToV1 ? this.StartInfo.Edge.V0 : this.StartInfo.Edge.V1;
			int v1 = this.StartInfo.ToV1 ? this.StartInfo.Edge.V1 : this.StartInfo.Edge.V0;

			// easiest way to get v2 is to use this function which requires the
			// other indices to already be in the list.
			scratchIndices.Add((ushort)v0);
			scratchIndices.Add((ushort)v1);

			int v2 = StripInfo.GetNextIndex(scratchIndices, this.StartInfo.Face);

			scratchIndices.Add((ushort)v2);

			// build the forward list
			int nv0 = v1;
			int nv1 = v2;

			var nextFace = Stripifier.FindFaceInfo(edgeInfos, nv0, nv1, this.StartInfo.Face);

			while (!(nextFace is null) && !this.IsMarked(nextFace))
			{
				// check to see if this next face is going to cause us to die soon
				int testnv0 = nv1;
				int testnv1 = StripInfo.GetNextIndex(scratchIndices, nextFace);

				var nextNextFace = Stripifier.FindFaceInfo(edgeInfos, testnv0, testnv1, nextFace);

				if (nextNextFace is null || this.IsMarked(nextNextFace))
				{
					// uh, oh, we're following a dead end, try swapping
					var testNextFace = Stripifier.FindFaceInfo(edgeInfos, nv0, testnv1, nextFace);

					if (!(testNextFace is null) && !this.IsMarked(testNextFace))
					{
						// we only swap if it buys us something

						// add a "fake" degenerate face
						var tempFace = new FaceInfo(nv0, nv1, nv0, true);

						forwardFaces.Add(tempFace);
						this.MarkTriangle(tempFace);

						scratchIndices.Add((ushort)nv0);
						testnv0 = nv0;

						++this.NumDegenerates;
					}
				}

				// add this to the strip
				forwardFaces.Add(nextFace);

				this.MarkTriangle(nextFace);

				// add the index
				// nv0 = nv1;
				// nv1 = Stripifier.GetNextIndex(scratchIndices, nextFace);
				scratchIndices.Add((ushort)testnv1);

				// and get the next face
				nv0 = testnv0;
				nv1 = testnv1;

				nextFace = Stripifier.FindFaceInfo(edgeInfos, nv0, nv1, nextFace);
			}

			// tempAllFaces is going to be forwardFaces + backwardFaces
			// it's used for Unique()
			var tempAllFaces = new List<FaceInfo>(forwardFaces);

			// reset the indices for building the strip backwards and do so
			scratchIndices.Clear();

			scratchIndices.Add((ushort)v2);
			scratchIndices.Add((ushort)v1);
			scratchIndices.Add((ushort)v0);

			nv0 = v1;
			nv1 = v0;

			nextFace = Stripifier.FindFaceInfo(edgeInfos, nv0, nv1, this.StartInfo.Face);

			while (!(nextFace is null) && !this.IsMarked(nextFace))
			{
				// this tests to see if a face is "unique", meaning that its vertices aren't already in the list
				// so, strips which "wrap-around" are not allowed
				if (!this.Unique(tempAllFaces, nextFace))
				{
					break;
				}

				// check to see if this next face is going to cause us to die soon
				int testnv0 = nv1;
				int testnv1 = StripInfo.GetNextIndex(scratchIndices, nextFace);

				var nextNextFace = Stripifier.FindFaceInfo(edgeInfos, testnv0, testnv1, nextFace) as FaceInfo;

				if (nextNextFace is null || this.IsMarked(nextNextFace))
				{
					// uh, oh, we're following a dead end, try swapping
					var testNextFace = Stripifier.FindFaceInfo(edgeInfos, nv0, testnv1, nextFace) as FaceInfo;

					if (!(testNextFace is null) && !this.IsMarked(testNextFace))
					{
						// we only swap if it buys us something

						// add a "fake" degenerate face
						var tempFace = new FaceInfo(nv0, nv1, nv0, true);

						backwardFaces.Add(tempFace);
						this.MarkTriangle(tempFace);

						scratchIndices.Add((ushort)nv0);
						testnv0 = nv0;

						++this.NumDegenerates;
					}

				}

				// add this to the strip
				backwardFaces.Add(nextFace);

				// this is just so Unique() will work
				tempAllFaces.Add(nextFace);

				this.MarkTriangle(nextFace);

				// add the index
				// nv0 = nv1;
				// nv1 = Stripifier.GetNextIndex(scratchIndices, nextFace);
				scratchIndices.Add((ushort)testnv1);

				// and get the next face
				nv0 = testnv0;
				nv1 = testnv1;
				nextFace = Stripifier.FindFaceInfo(edgeInfos, nv0, nv1, nextFace);
			}

			// Combine the forward and backwards stripification lists and put into our own face vector
			this.Combine(forwardFaces, backwardFaces);
		}
	}
}
