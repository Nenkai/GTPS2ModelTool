using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpTriStrip
{
	/// <summary>
	/// Provides methods for creating strip arrays out of index datas. Used internally only.
	/// </summary>
	public class Stripifier
	{
		private ref struct TriBoolean
		{
			public bool B0;
			public bool B1;
			public bool B2;
		}

		private const int CACHE_INEFFICIENCY = 6;

		private readonly List<ushort> m_indices;
		private int m_cacheSize;
		private int m_minStripLength;
		private float m_meshJump;
		private bool m_firstTimeResetPoint;

		/// <summary>
		/// Creates a new instance <see cref="Stripifier"/>.
		/// </summary>
		public Stripifier() => this.m_indices = new List<ushort>();

		private static int GetUniqueVertexInB(FaceInfo faceA, FaceInfo faceB)
		{
			int facev0 = faceB.V0;

			if (facev0 != faceA.V0 && facev0 != faceA.V1 && facev0 != faceA.V2)
			{
				return facev0;
			}

			int facev1 = faceB.V1;

			if (facev1 != faceA.V0 && facev1 != faceA.V1 && facev1 != faceA.V2)
			{
				return facev1;
			}

			int facev2 = faceB.V2;

			if (facev2 != faceA.V0 && facev2 != faceA.V1 && facev2 != faceA.V2)
			{
				return facev2;
			}

			return -1;
		}

		private static void GetSharedVertices(FaceInfo faceA, FaceInfo faceB, out int vertex0, out int vertex1)
		{
			vertex0 = -1;
			vertex1 = -1;

			int facev0 = faceB.V0;

			if (facev0 == faceA.V0 || facev0 == faceA.V1 || facev0 == faceA.V2)
			{
				if (vertex0 == -1)
				{
					vertex0 = facev0;
				}
				else
				{
					vertex1 = facev0;
					return;
				}
			}

			int facev1 = faceB.V1;

			if (facev1 == faceA.V0 || facev1 == faceA.V1 || facev1 == faceA.V2)
			{
				if (vertex0 == -1)
				{
					vertex0 = facev1;
				}
				else
				{
					vertex1 = facev1;
					return;
				}
			}

			int facev2 = faceB.V2;

			if (facev2 == faceA.V0 || facev2 == faceA.V1 || facev2 == faceA.V2)
			{
				if (vertex0 == -1)
				{
					vertex0 = facev2;
				}
				else
				{
					vertex1 = facev2;
					return;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsCW(FaceInfo faceInfo, int v0, int v1)
		{
			if (faceInfo.V0 == v0)
			{
				return faceInfo.V1 == v1;
			}
			else if (faceInfo.V1 == v0)
			{
				return faceInfo.V2 == v1;
			}
			else
			{
				return faceInfo.V0 == v1;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool NextIsCW(int numIndices)
		{
			return (numIndices & 1) == 0;
		}

		private static bool FindTraversal(List<EdgeInfo> edgeInfos, StripInfo strip, out StripStartInfo startInfo)
		{
			// if the strip was v0->v1 on the edge, then v1 will be a vertex in the next edge.
			int v = strip.StartInfo.ToV1 ? strip.StartInfo.Edge.V1 : strip.StartInfo.Edge.V0;

			FaceInfo untouchedFace = null;
			EdgeInfo edgeIter = edgeInfos[v];

			while (!(edgeIter is null))
			{
				var face0 = edgeIter.Face0;
				var face1 = edgeIter.Face1;

				if (!(face0 is null) && !strip.IsInStrip(face0) && !(face1 is null) && !strip.IsMarked(face1))
				{
					untouchedFace = face1;
					break;
				}

				if (!(face1 is null) && !strip.IsInStrip(face1) && !(face0 is null) && !strip.IsMarked(face0))
				{
					untouchedFace = face0;
					break;
				}

				// find the next edgeIter
				edgeIter = edgeIter.V0 == v ? edgeIter.Next0 : edgeIter.Next1;
			}

			if (!(edgeIter is null))
			{
				if (strip.SharesEdge(untouchedFace, edgeInfos))
				{
					startInfo = new StripStartInfo(untouchedFace, edgeIter, edgeIter.V0 == v);
				}
				else
				{
					startInfo = new StripStartInfo(untouchedFace, edgeIter, edgeIter.V1 == v);
				}
			}
			else
			{
				startInfo = new StripStartInfo(untouchedFace, edgeIter, false);
			}

			return !(startInfo.Face is null);
		}

		private static float AvgStripSize(List<StripInfo> strips)
		{
			int sizeAccum = 0;
			int numStrips = strips.Count;

			for (int i = 0; i < numStrips; ++i)
			{
				var strip = strips[i];
				sizeAccum += strip.FaceInfos.Count;
				sizeAccum -= strip.NumDegenerates;
			}

			return ((float)sizeAccum) / ((float)numStrips);
		}

		private static int FindStartPoint(List<FaceInfo> faceInfos, List<EdgeInfo> edgeInfos)
		{
			int bestCtr = -1;
			int bestIndex = -1;

			for (int i = 0; i < faceInfos.Count; ++i)
			{
				int ctr = 0;

				var faceInfo = faceInfos[i];

				if (Stripifier.FindFaceInfo(edgeInfos, faceInfo.V0, faceInfo.V1, faceInfo) is null)
				{
					++ctr;
				}
				
				if (Stripifier.FindFaceInfo(edgeInfos, faceInfo.V1, faceInfo.V2, faceInfo) is null)
				{
					++ctr;
				}
				
				if (Stripifier.FindFaceInfo(edgeInfos, faceInfo.V2, faceInfo.V0, faceInfo) is null)
				{
					++ctr;
				}

				if (ctr > bestCtr)
				{
					bestCtr = ctr;
					bestIndex = i;
					// return i;
				}
			}

			// return -1;

			return bestCtr == 0 ? -1 : bestIndex;
		}

		private static void UpdateCacheStrip(VertexCache vcache, StripInfo strip)
		{
			for (int i = 0; i < strip.FaceInfos.Count; ++i)
			{
				var faceInfo = strip.FaceInfos[i];

				if (!vcache.InCache(faceInfo.V0))
				{
					vcache.AddEntry(faceInfo.V0);
				}

				if (!vcache.InCache(faceInfo.V1))
				{
					vcache.AddEntry(faceInfo.V1);
				}

				if (!vcache.InCache(faceInfo.V2))
				{
					vcache.AddEntry(faceInfo.V2);
				}
			}
		}

		private static void UpdateCacheFace(VertexCache vcache, FaceInfo face)
		{
			if (!vcache.InCache(face.V0))
			{
				vcache.AddEntry(face.V0);
			}

			if (!vcache.InCache(face.V1))
			{
				vcache.AddEntry(face.V1);
			}

			if (!vcache.InCache(face.V2))
			{
				vcache.AddEntry(face.V2);
			}
		}

		private static float CalcNumHitsStrip(VertexCache vcache, StripInfo strip)
		{
			int numHits = 0;
			int numFaces = 0;

			for (int i = 0; i < strip.FaceInfos.Count; ++i)
			{
				var faceInfo = strip.FaceInfos[i];

				if (vcache.InCache(faceInfo.V0))
				{
					++numHits;
				}

				if (vcache.InCache(faceInfo.V1))
				{
					++numHits;
				}

				if (vcache.InCache(faceInfo.V2))
				{
					++numHits;
				}

				++numFaces;
			}

			return (float)numHits / (float)numFaces;
		}

		private static int CalcNumHitsFace(VertexCache vcache, FaceInfo face)
		{
			int numHits = 0;

			if (vcache.InCache(face.V0))
			{
				++numHits;
			}

			if (vcache.InCache(face.V1))
			{
				++numHits;
			}

			if (vcache.InCache(face.V2))
			{
				++numHits;
			}

			return numHits;
		}

		private static int NumNeighbors(FaceInfo face, List<EdgeInfo> edgeInfoVec)
		{
			int numNeighbors = 0;

			if (!(Stripifier.FindFaceInfo(edgeInfoVec, face.V0, face.V1, face) is null))
			{
				++numNeighbors;
			}

			if (!(Stripifier.FindFaceInfo(edgeInfoVec, face.V1, face.V2, face) is null))
			{
				++numNeighbors;
			}

			if (!(Stripifier.FindFaceInfo(edgeInfoVec, face.V2, face.V0, face) is null))
			{
				++numNeighbors;
			}

			return numNeighbors;
		}

		private static bool AlreadyExists(FaceInfo faceInfo, List<FaceInfo> faceInfos)
		{
			for (int i = 0; i < faceInfos.Count; ++i)
			{
				if ((faceInfos[i].V0 == faceInfo.V0) &&
					(faceInfos[i].V1 == faceInfo.V1) &&
					(faceInfos[i].V2 == faceInfo.V2))
				{
					return true;
				}
			}

			return false;
		}

		private FaceInfo FindGoodResetPoint(List<FaceInfo> faceInfos, List<EdgeInfo> edgeInfos)
		{
			// we hop into different areas of the mesh to try to get
			// other large open spans done.  Areas of small strips can
			// just be left to triangle lists added at the end.
			FaceInfo result = null;

			if (result is null)
			{
				int numFaces = faceInfos.Count;
				int startPoint;

				if (this.m_firstTimeResetPoint)
				{
					// first time, find a face with few neighbors (look for an edge of the mesh)
					startPoint = Stripifier.FindStartPoint(faceInfos, edgeInfos);
					this.m_firstTimeResetPoint = false;
				}
				else
				{
					startPoint = (int)(((float)numFaces - 1) * this.m_meshJump);
				}

				if (startPoint == -1)
				{
					startPoint = (int)(((float)numFaces - 1) * this.m_meshJump);

					// this.m_meshJump += 0.1f;
					// 
					// if (this.m_meshJump > 1.0f)
					// {
					// 	  this.m_meshJump = 0.05f;
					// }
				}

				int i = startPoint;

				do
				{
					// if this guy isn't visited, try him
					var tryInfo = faceInfos[i];

					if (tryInfo.StripID < 0)
					{
						result = tryInfo;
						break;
					}

					// update the index and clamp to 0-(numFaces-1)
					if (++i >= numFaces)
					{
						i = 0;
					}

				} while (i != startPoint);

				// update the meshJump
				this.m_meshJump += 0.1f;

				if (this.m_meshJump > 1.0f)
				{
					this.m_meshJump = 0.05f;
				}
			}

			// return the best face we found
			return result;
		}

		private void FindAllStrips(List<FaceInfo> allFaceInfos, List<EdgeInfo> allEdgeInfos, int numSamples, out List<StripInfo> allStrips)
		{
			// initialize
			allStrips = new List<StripInfo>();

			// the experiments
			int experimentId = 0;
			int stripId = 0;
			var done = false;

			int loopCtr = 0;

			while (!done)
			{
				++loopCtr;

				// PHASE 1: Set up numSamples * numEdges experiments

				int experimentIndex = 0;
				var experiments = new List<StripInfo>[numSamples * 6];
				var resetPoints = new HashSet<FaceInfo>(); // std::set

				for (int i = 0; i < experiments.Length; ++i)
				{
					experiments[i] = new List<StripInfo>();
				}

				for (int i = 0; i < numSamples; ++i)
				{
					// Try to find another good reset point.
					// If there are none to be found, we are done
					var nextFace = this.FindGoodResetPoint(allFaceInfos, allEdgeInfos);

					if (nextFace is null)
					{
						done = true;
						break;
					}

					// If we have already evaluated starting at this face in this slew
					// of experiments, then skip going any further
					if (!resetPoints.Add(nextFace))
					{
						continue;
					}

					// otherwise, we shall now try experiments for starting on the 0->1, 1->2, and 2->0 edges
					Debug.Assert(nextFace.StripID < 0);

					// build the strip off of this face's 0-1 edge
					var edge01 = Stripifier.FindEdgeInfo(allEdgeInfos, nextFace.V0, nextFace.V1);
					var strip01 = new StripInfo(new StripStartInfo(nextFace, edge01, true), stripId++, experimentId++);
					experiments[experimentIndex++].Add(strip01);

					// build the strip off of this face's 1-0 edge
					var edge10 = Stripifier.FindEdgeInfo(allEdgeInfos, nextFace.V0, nextFace.V1);
					var strip10 = new StripInfo(new StripStartInfo(nextFace, edge10, false), stripId++, experimentId++);
					experiments[experimentIndex++].Add(strip10);

					// build the strip off of this face's 1-2 edge
					var edge12 = Stripifier.FindEdgeInfo(allEdgeInfos, nextFace.V1, nextFace.V2);
					var strip12 = new StripInfo(new StripStartInfo(nextFace, edge12, true), stripId++, experimentId++);
					experiments[experimentIndex++].Add(strip12);

					// build the strip off of this face's 2-1 edge
					var edge21 = Stripifier.FindEdgeInfo(allEdgeInfos, nextFace.V1, nextFace.V2);
					var strip21 = new StripInfo(new StripStartInfo(nextFace, edge21, false), stripId++, experimentId++);
					experiments[experimentIndex++].Add(strip21);

					// build the strip off of this face's 2-0 edge
					var edge20 = Stripifier.FindEdgeInfo(allEdgeInfos, nextFace.V2, nextFace.V0);
					var strip20 = new StripInfo(new StripStartInfo(nextFace, edge20, true), stripId++, experimentId++);
					experiments[experimentIndex++].Add(strip20);

					// build the strip off of this face's 0-2 edge
					var edge02 = Stripifier.FindEdgeInfo(allEdgeInfos, nextFace.V2, nextFace.V0);
					var strip02 = new StripInfo(new StripStartInfo(nextFace, edge02, false), stripId++, experimentId++);
					experiments[experimentIndex++].Add(strip02);
				}

				// PHASE 2: Iterate through that we setup in the last phase and really 
				// build each of the strips and strips that follow to see how far we get

				int numExperiments = experimentIndex;

				for (int i = 0; i < numExperiments; ++i)
				{
					// get the strip set

					// build the first strip of the list
					experiments[i][0].Build(allEdgeInfos);
					int experimentID = experiments[i][0].ExperimentID;

					var stripIter = experiments[i][0];

					while (Stripifier.FindTraversal(allEdgeInfos, stripIter, out var startInfo))
					{
						// create the new strip info
						stripIter = new StripInfo(startInfo, stripId++, experimentID);

						// build the next strip
						stripIter.Build(allEdgeInfos);

						// add it to the list
						experiments[i].Add(stripIter);
					}
				}

				// Phase 3: Find the experiment that has the most promise

				int bestIndex = 0;
				var bestValue = 0.0f;

				for (int i = 0; i < numExperiments; ++i)
				{
					const float avgStripSizeWeight = 1.0f;
					const float numStripsWeight = 0.0f;
					// const float numTrisWeight = 0.0f;
					float avgStripSize = Stripifier.AvgStripSize(experiments[i]);
					float numStrips = experiments[i].Count;
					float value = avgStripSize * avgStripSizeWeight + (numStrips * numStripsWeight);
					// float value = 1.0f / numStrips;
					// float value = numStrips * avgStripSize;

					if (value > bestValue)
					{
						bestValue = value;
						bestIndex = i;
					}
				}

				// Phase 4: commit the best experiment of the bunch

				this.CommitStrips(allStrips, experiments[bestIndex]);

				for (int i = 0; i < numExperiments; ++i)
				{
					if (i != bestIndex)
					{
						int numStrips = experiments[i].Count;

						for (int j = 0; j < numStrips; ++j)
						{
							var currStrip = experiments[i][j];

							for (int k = 0; k < currStrip.FaceInfos.Count; ++k)
							{
								if (currStrip.FaceInfos[k].IsFake)
								{
									currStrip.FaceInfos[k] = null;
								}
							}

							experiments[i][j] = null;
						}
					}
				}
			}
		}

		private void SplitUpStripsAndOptimize(List<StripInfo> allStrips, List<EdgeInfo> edgeInfos, out List<StripInfo> outStrips, out List<FaceInfo> outFaceList)
		{
			int threshold = this.m_cacheSize;
			var tempStrips = new List<StripInfo>();

			// split up strips into threshold-sized pieces
			for (int i = 0; i < allStrips.Count; ++i)
			{
				StripInfo currentStrip;

				int actualStripSize = 0;
				var declStrip = allStrips[i];

				for (int j = 0; j < declStrip.FaceInfos.Count; ++j)
				{
					if (!Stripifier.IsDegenerate(declStrip.FaceInfos[j]))
					{
						++actualStripSize;
					}
				}

				if (actualStripSize /* declStrip.FaceInfos.Count */ > threshold)
				{
					int numTimes = actualStripSize /* declStrip.FaceInfos.Count */ / threshold;
					int numLeftover = actualStripSize /* declStrip.FaceInfos.Count */ % threshold;

					int degenerateCount = 0;

					for (int j = 0; j < numTimes; ++j)
					{
						currentStrip = new StripInfo(new StripStartInfo(null, null, false), 0);

						int faceCtr = j * threshold + degenerateCount;
						var firstTime = true;

						while (faceCtr < threshold + (j * threshold) + degenerateCount)
						{
							if (Stripifier.IsDegenerate(declStrip.FaceInfos[faceCtr]))
							{
								++degenerateCount;

								// last time or first time through, no need for a degenerate
								if ((((faceCtr + 1) != threshold + (j * threshold) + degenerateCount) ||
									 ((j == numTimes - 1) && (numLeftover < 4) && (numLeftover > 0))) &&
									 !firstTime)
								{
									currentStrip.FaceInfos.Add(declStrip.FaceInfos[faceCtr++]);
								}
								else
								{
									// but, we do need to delete the degenerate, if it's marked fake, to avoid leaking
									if (declStrip.FaceInfos[faceCtr].IsFake)
									{
										declStrip.FaceInfos[faceCtr] = null;
									}

									++faceCtr;
								}
							}
							else
							{
								currentStrip.FaceInfos.Add(declStrip.FaceInfos[faceCtr++]);
								firstTime = false;
							}
						}

						// for (int faceCtr2 = j * threshold; faceCtr2 < threshold + (j * threshold); ++faceCtr2)
						// {
						//     currentStrip.FaceInfos.Add(declStrip.FaceInfos[faceCtr2]);
						// }

						if (j == numTimes - 1) // last time through
						{
							if (numLeftover > 0 && numLeftover < 4) // way too small
							{
								// just add to last strip
								int ctr = 0;

								while (ctr < numLeftover)
								{
									if (Stripifier.IsDegenerate(declStrip.FaceInfos[faceCtr]))
									{
										++degenerateCount;
									}
									else
									{
										++ctr;
									}

									currentStrip.FaceInfos.Add(declStrip.FaceInfos[faceCtr++]);
								}

								numLeftover = 0;
							}
						}

						tempStrips.Add(currentStrip);
					}

					int leftOff = numTimes * threshold + degenerateCount;

					if (numLeftover != 0)
					{
						currentStrip = new StripInfo(new StripStartInfo(null, null, false), 0);

						int ctr = 0;
						bool firstTime = true;

						while (ctr < numLeftover)
						{
							if (!Stripifier.IsDegenerate(declStrip.FaceInfos[leftOff]))
							{
								++ctr;
								firstTime = false;
								currentStrip.FaceInfos.Add(declStrip.FaceInfos[leftOff++]);
							}
							else if (!firstTime)
							{
								currentStrip.FaceInfos.Add(declStrip.FaceInfos[leftOff++]);
							}
							else
							{
								// don't leak
								if (declStrip.FaceInfos[leftOff].IsFake)
								{
									declStrip.FaceInfos[leftOff] = null;
								}

								++leftOff;
							}
						}

						// for (int k = 0; k < numLeftover; ++k)
						// {
						//     currentStrip.FaceInfos.Add(declStrip.FaceInfos[leftOff++]);
						// }

						tempStrips.Add(currentStrip);
					}
				}
				else
				{
					// we're not just doing a tempStrips.Add(allBigStrips[i]) because
					// this way we can clear allBigStrips later to free the memory
					currentStrip = new StripInfo(new StripStartInfo(null, null, false), 0);

					for (int j = 0; j < allStrips[i].FaceInfos.Count; ++j)
					{
						currentStrip.FaceInfos.Add(allStrips[i].FaceInfos[j]);
					}

					tempStrips.Add(currentStrip);
				}
			}

			// add small strips to face list
			this.RemoveSmallStrips(tempStrips, out var tempStrips2, out outFaceList);

			// make new result strip list
			outStrips = new List<StripInfo>();

			// screw optimization for now
			// for (int i = 0; i < tempStrips.Count; ++i)
			// {
			//     outStrips.Add(tempStrips[i]);
			// }

			if (tempStrips2.Count != 0)
			{
				// Optimize for the vertex cache
				var vcache = new VertexCache(this.m_cacheSize);

				float bestNumHits = -1.0f;
				float numHits;
				int bestIndex = -1;

				int firstIndex = 0;
				float minCost = 10000.0f;

				for (int i = 0; i < tempStrips2.Count; ++i)
				{
					int numNeighbors = 0;
					var declStrip = tempStrips2[i];

					//find strip with least number of neighbors per face
					for (int j = 0; j < declStrip.FaceInfos.Count; ++j)
					{
						numNeighbors += Stripifier.NumNeighbors(declStrip.FaceInfos[j], edgeInfos);
					}

					var currCost = (float)numNeighbors / (float)declStrip.FaceInfos.Count;

					if (currCost < minCost)
					{
						minCost = currCost;
						firstIndex = i;
					}
				}

				Stripifier.UpdateCacheStrip(vcache, tempStrips2[firstIndex]);
				outStrips.Add(tempStrips2[firstIndex]);

				tempStrips2[firstIndex].Visited = true;

				bool wantsCW = (tempStrips2[firstIndex].FaceInfos.Count & 1) == 0;

				// this n^2 algorithm is what slows down stripification so much....
				// needs to be improved

				while (true)
				{
					bestNumHits = -1.0f;

					// find best strip to add next, given the current cache
					for (int i = 0; i < tempStrips2.Count; ++i)
					{
						var declStrip = tempStrips2[i];

						if (declStrip.Visited)
						{
							continue;
						}

						numHits = Stripifier.CalcNumHitsStrip(vcache, declStrip);

						if (numHits > bestNumHits)
						{
							bestNumHits = numHits;
							bestIndex = i;
						}
						else if (numHits == bestNumHits)
						{
							// check previous strip to see if this one requires it to switch polarity
							int nStripFaceCount = declStrip.FaceInfos.Count;

							var beginFace = declStrip.FaceInfos[0]; // create copy
							var firstFace = new FaceInfo(beginFace.V0, beginFace.V1, beginFace.V2);

							// If there is a second face, reorder vertices such that the
							// unique vertex is first

							if (nStripFaceCount > 1)
							{
								int nUnique = Stripifier.GetUniqueVertexInB(declStrip.FaceInfos[1], firstFace);

								if (nUnique == firstFace.V1)
								{
									var temp = firstFace.V0;
									firstFace.V0 = firstFace.V1;
									firstFace.V1 = temp;
								}
								else if (nUnique == firstFace.V2)
								{
									var temp = firstFace.V0;
									firstFace.V0 = firstFace.V2;
									firstFace.V2 = temp;
								}

								// If there is a third face, reorder vertices such that the
								// shared vertex is last
								if (nStripFaceCount > 2)
								{
									Stripifier.GetSharedVertices(declStrip.FaceInfos[2], firstFace, out int nShared0, out int nShared1);

									if ((nShared0 == firstFace.V1) && (nShared1 == -1))
									{
										var temp = firstFace.V1;
										firstFace.V1 = firstFace.V2;
										firstFace.V2 = temp;
									}
								}
							}

							// Check CW/CCW ordering
							if (wantsCW == Stripifier.IsCW(beginFace, firstFace.V0, firstFace.V1))
							{
								bestIndex = i; // I like this one!
							}
						}
					}

					if (bestNumHits == -1.0f)
					{
						break;
					}

					var bestStrip = tempStrips2[bestIndex];

					bestStrip.Visited = true;
					Stripifier.UpdateCacheStrip(vcache, bestStrip);

					outStrips.Add(bestStrip);
					wantsCW = (bestStrip.FaceInfos.Count & 1) == 0 ? wantsCW : !wantsCW;
				}
			}
		}

		private void RemoveSmallStrips(List<StripInfo> allStrips, out List<StripInfo> allBigStrips, out List<FaceInfo> faceList)
		{
			faceList = new List<FaceInfo>();
			allBigStrips = new List<StripInfo>();
			var tempFaceList = new List<FaceInfo>();

			for (int i = 0; i < allStrips.Count; ++i)
			{
				if (allStrips[i].FaceInfos.Count < this.m_minStripLength)
				{
					// strip is too small, add faces to faceList
					for (int j = 0; j < allStrips[i].FaceInfos.Count; ++j)
					{
						tempFaceList.Add(allStrips[i].FaceInfos[j]);
					}

					allStrips[i] = null;
				}
				else
				{
					allBigStrips.Add(allStrips[i]);
				}
			}

			if (tempFaceList.Count > 0)
			{
				var visitedList = new bool[tempFaceList.Count];
				var vcache = new VertexCache(this.m_cacheSize);

				int bestNumHits = -1;
				int bestIndex = -1;

				while (true)
				{
					bestNumHits = -1;

					//find best face to add next, given the current cache
					for (int i = 0; i < tempFaceList.Count; ++i)
					{
						if (visitedList[i])
						{
							continue;
						}

						var numHits = Stripifier.CalcNumHitsFace(vcache, tempFaceList[i]);

						if (numHits > bestNumHits)
						{
							bestNumHits = numHits;
							bestIndex = i;
						}
					}

					if (bestNumHits == -1)
					{
						break;
					}

					visitedList[bestIndex] = true;
					Stripifier.UpdateCacheFace(vcache, tempFaceList[bestIndex]);
					faceList.Add(tempFaceList[bestIndex]);
				}
			}
		}

		private void CommitStrips(List<StripInfo> allStrips, List<StripInfo> strips)
		{
			// Iterate through strips
			int numStrips = strips.Count;

			for (int i = 0; i < numStrips; ++i)
			{
				// Tell the strip that it is now real
				var strip = strips[i];
				strip.ExperimentID = -1;

				// add to the list of real strips
				allStrips.Add(strip);

				// Iterate through the faces of the strip
				// Tell the faces of the strip that they belong to a real strip now
				var faces = strips[i].FaceInfos;
				int numFaces = faces.Count;

				for (int j = 0; j < numFaces; ++j)
				{
					strip.MarkTriangle(faces[j]);
				}
			}
		}

		private void BuildStripifyInfo(ushort maxIndex, out List<FaceInfo> faceInfos, out List<EdgeInfo> edgeInfos, Action<string> debugWriter = null)
		{
			// reserve space for the face infos, but do not resize them.
			var debugNull = debugWriter is null;
			int numIndices = this.m_indices.Count;
			int numTriangles = numIndices / 3;

			faceInfos = new List<FaceInfo>(numTriangles);
			edgeInfos = new List<EdgeInfo>(maxIndex + 1);
			
			for (int i = 0; i < maxIndex + 1; ++i)
			{
				edgeInfos.Add(null);
			}

			// iterate through the triangles of the triangle list
			int index = 0;
			var faceUpdated = new TriBoolean();

			for (int i = 0; i < numTriangles; ++i)
			{
				bool mightAlreadyExist = true;

				faceUpdated.B0 = false;
				faceUpdated.B1 = false;
				faceUpdated.B2 = false;

				// grab the indices
				var v0 = this.m_indices[index++];
				var v1 = this.m_indices[index++];
				var v2 = this.m_indices[index++];

				// we disregard degenerates
				if (Stripifier.IsDegenerate(v0, v1, v2))
				{
					continue;
				}

				// create the face info and add it to the list of faces, but only if this exact face doesn't already 
				// exist in the list
				var faceInfo = new FaceInfo(v0, v1, v2);

				// grab the edge infos, creating them if they do not already exist
				var edgeInfo01 = Stripifier.FindEdgeInfo(edgeInfos, v0, v1);

				if (edgeInfo01 is null)
				{
					// since one of it's edges isn't in the edge data structure, it can't already exist in the face structure
					mightAlreadyExist = false;

					// create the info
					edgeInfo01 = new EdgeInfo(v0, v1)
					{
						Next0 = edgeInfos[v0],
						Next1 = edgeInfos[v1],
					};

					edgeInfos[v0] = edgeInfo01;
					edgeInfos[v1] = edgeInfo01;

					// set face 0
					edgeInfo01.Face0 = faceInfo;
				}
				else
				{
					if (!(edgeInfo01.Face1 is null))
					{
						if (!debugNull)
						{
							debugWriter.Invoke("BuildStripifyInfo: > 2 triangles on an edge... uncertain consequences");
						}
					}
					else
					{
						edgeInfo01.Face1 = faceInfo;
						faceUpdated.B0 = true;
					}
				}

				// grab the edge infos, creating them if they do not already exist
				var edgeInfo12 = Stripifier.FindEdgeInfo(edgeInfos, v1, v2);

				if (edgeInfo12 is null)
				{
					mightAlreadyExist = false;

					// create the info
					edgeInfo12 = new EdgeInfo(v1, v2)
					{
						Next0 = edgeInfos[v1],
						Next1 = edgeInfos[v2],
					};

					edgeInfos[v1] = edgeInfo12;
					edgeInfos[v2] = edgeInfo12;

					// set face 0
					edgeInfo12.Face0 = faceInfo;
				}
				else
				{
					if (!(edgeInfo12.Face1 is null))
					{
						if (!debugNull)
						{
							debugWriter.Invoke("BuildStripifyInfo: > 2 triangles on an edge... uncertain consequences");
						}
					}
					else
					{
						edgeInfo12.Face1 = faceInfo;
						faceUpdated.B1 = true;
					}
				}

				// grab the edge infos, creating them if they do not already exist
				var edgeInfo20 = Stripifier.FindEdgeInfo(edgeInfos, v2, v0);

				if (edgeInfo20 is null)
				{
					mightAlreadyExist = false;

					// create the info
					edgeInfo20 = new EdgeInfo(v2, v0)
					{
						Next0 = edgeInfos[v2],
						Next1 = edgeInfos[v0],
					};
					
					edgeInfos[v2] = edgeInfo20;
					edgeInfos[v0] = edgeInfo20;

					// set face 0
					edgeInfo20.Face0 = faceInfo;
				}
				else
				{
					if (!(edgeInfo20.Face1 is null))
					{
						if (!debugNull)
						{
							debugWriter.Invoke("BuildStripifyInfo: > 2 triangles on an edge... uncertain consequences");
						}
					}
					else
					{
						edgeInfo20.Face1 = faceInfo;
						faceUpdated.B2 = true;
					}
				}

				if (mightAlreadyExist)
				{
					if (!Stripifier.AlreadyExists(faceInfo, faceInfos))
					{
						faceInfos.Add(faceInfo);
					}
					else
					{
						// cleanup pointers that point to this deleted face
						if (faceUpdated.B0)
						{
							edgeInfo01.Face1 = null;
						}
						
						if (faceUpdated.B1)
						{
							edgeInfo12.Face1 = null;
						}
						
						if (faceUpdated.B2)
						{
							edgeInfo20.Face1 = null;
						}
					}
				}
				else
				{
					faceInfos.Add(faceInfo);
				}
			}
		}

		/// <summary>
		/// Checks if <see cref="FaceInfo"/> is degenerate, meaning has 2 or more shared vertices.
		/// </summary>
		/// <param name="face"><see cref="FaceInfo"/> to check.</param>
		/// <returns><see langword="true"/> if <see cref="FaceInfo"/> provided has 2 or more shared vertices;
		/// otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDegenerate(FaceInfo face)
		{
			return face.V0 == face.V1 || face.V1 == face.V2 || face.V2 == face.V0;
		}

		/// <summary>
		/// Checks if indices form a degenerate face, meaning one that has 2 or more shared vertices.
		/// </summary>
		/// <param name="v0">First index of the face.</param>
		/// <param name="v1">Second index of the face.</param>
		/// <param name="v2">Third index of the face.</param>
		/// <returns><see langword="true"/> if indices form a degenerate face; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDegenerate(ushort v0, ushort v1, ushort v2)
		{
			return v0 == v1 || v1 == v2 || v2 == v0;
		}

		/// <summary>
		/// Finds the <see cref="EdgeInfo"/> using two indices provided.
		/// </summary>
		/// <param name="edgeInfos"><see cref="List{T}"/> of type <see cref="EdgeInfo"/> that contains all
		/// edge information of faces and strips.</param>
		/// <param name="v0">First index of <see cref="EdgeInfo"/> to search for.</param>
		/// <param name="v1">Second index of <see cref="EdgeInfo"/> to search for.</param>
		/// <returns><see cref="EdgeInfo"/> that consists of two indices given, if exists; otherwise, <see langword="null"/>.</returns>
		public static EdgeInfo FindEdgeInfo(List<EdgeInfo> edgeInfos, int v0, int v1)
		{
			// we can get to it through either array
			// because the edge infos have a v0 and v1
			// and there is no order except how it was
			// first created.

			var infoIter = edgeInfos[v0];

			while (!(infoIter is null))
			{
				if (infoIter.V0 == v0)
				{
					if (infoIter.V1 == v1)
					{
						return infoIter;
					}
					else
					{
						infoIter = infoIter.Next0;
					}
				}
				else
				{
					Debug.Assert(infoIter.V1 == v0);

					if (infoIter.V0 == v1)
					{
						return infoIter;
					}
					else
					{
						infoIter = infoIter.Next1;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Finds the <see cref="FaceInfo"/> using <see cref="List{T}"/> of <see cref="EdgeInfo"/> and
		/// two indices that form one of the face's edges. Note that this method will attempt to return 
		/// <see cref="FaceInfo"/> that is different from the <paramref name="faceInfo"/>.
		/// </summary>
		/// <param name="edgeInfos"><see cref="List{T}"/> of type <see cref="EdgeInfo"/> that contains all
		/// edge information of faces and strips.</param>
		/// <param name="v0">First index of <see cref="FaceInfo"/> to search for.</param>
		/// <param name="v1">Second index of <see cref="FaceInfo"/> to search for.</param>
		/// <param name="faceInfo"><see cref="FaceInfo"/> to base on; if a unique <see cref="FaceInfo"/>
		/// exists that shares any two indices with this one, it is being prioritized as a return value.</param>
		/// <returns><see cref="FaceInfo"/> that contains two indices given, if exists; otherwise, <see langword="null"/>.</returns>
		public static FaceInfo FindFaceInfo(List<EdgeInfo> edgeInfos, int v0, int v1, FaceInfo faceInfo)
		{
			var edgeInfo = Stripifier.FindEdgeInfo(edgeInfos, v0, v1);

			if (edgeInfo is null && v0 == v1)
			{
				// we've hit a degenerate
				return null;
			}

			Debug.Assert(!(edgeInfo is null));

			return Object.ReferenceEquals(edgeInfo.Face0, faceInfo) ? edgeInfo.Face1 : edgeInfo.Face0;
		}

		/// <summary>
		/// Generates <see cref="List{T}"/> of type <see cref="StripInfo"/> using the input index list provided.
		/// </summary>
		/// <param name="indices">Input <see cref="IEnumerable{T}"/> of type <see cref="UInt16"/> that contains
		/// index buffer.</param>
		/// <param name="cacheSize">Cache size that will be used for <see cref="VertexCache"/>.</param>
		/// <param name="minStripLength">Minimum strip length that is considered when generating <paramref name="outStrips"/>;
		/// if length is smaller than this number, faces are being pushed into <paramref name="outFaceList"/>.</param>
		/// <param name="maxIndex">Maximum index encountered in <paramref name="indices"/>.</param>
		/// <param name="outStrips">Output <see cref="List{T}"/> of type <see cref="StripInfo"/> that contains all
		/// generated strips based on the input provided.</param>
		/// <param name="outFaceList">Output <see cref="List{T}"/> of type <see cref="FaceInfo"/> that contains separate
		/// <see cref="FaceInfo"/> that are not being included in strips because of <paramref name="minStripLength"/>.</param>
		public void Stripify(IEnumerable<ushort> indices, int cacheSize, int minStripLength,
			ushort maxIndex, out List<StripInfo> outStrips, out List<FaceInfo> outFaceList)
		{
			this.m_meshJump = 0.0f;
			this.m_firstTimeResetPoint = true; // used in FindGoodResetPoint()

			// the number of times to run the experiments
			int numSamples = 10;

			// the cache size, clamped to one
			this.m_cacheSize = Math.Max(1, cacheSize - CACHE_INEFFICIENCY);

			// this is the strip size threshold below which we dump the strip into a list
			this.m_minStripLength = minStripLength;

			this.m_indices.Clear();
			this.m_indices.AddRange(indices);

			// build the stripification info
			this.BuildStripifyInfo(maxIndex, out var allFaceInfos, out var allEdgeInfos);

			// stripify
			this.FindAllStrips(allFaceInfos, allEdgeInfos, numSamples, out var allStrips);

			// split up the strips into cache friendly pieces, optimize them, then dump these into outStrips
			this.SplitUpStripsAndOptimize(allStrips, allEdgeInfos, out outStrips, out outFaceList);
		}

		/// <summary>
		/// Generates actual strips from the list-in-strip-order. Note that if <paramref name="stitchStrips"/> is set
		/// to false, then strips in <paramref name="stripIndices"/> will be separated by the value of -1.
		/// </summary>
		/// <param name="allStrips"><see cref="List{T}"/> of type <see cref="StripInfo"/> that contains all
		/// strip information required for final output.</param>
		/// <param name="stitchStrips"><see langword="true"/> if all strips should be "stitched together", meaning
		/// combined into one unified strip; otherwise, <see langword="false"/>.</param>
		/// <param name="restart">For GPU enables primitive restart; note that it is meaningless when dealing
		/// with <paramref name="stitchStrips"/> set to <see langword="false"/>.</param>
		/// <param name="restartValue">Restart value in case <paramref name="restart"/> is set to <see langword="true"/>.</param>
		/// <param name="stripIndices">Output <see cref="List{T}"/> of type <see cref="Int32"/> that contains strip
		/// index buffers. Note that all strips are separated by the value of -1.</param>
		/// <param name="numSeparateStrips">Final number of strips generated at the output.</param>
		public void CreateStrips(List<StripInfo> allStrips, bool stitchStrips, bool restart, int restartValue, out List<int> stripIndices, out int numSeparateStrips)
		{
			numSeparateStrips = 0;
			stripIndices = new List<int>();

			var lastFace = new FaceInfo(0, 0, 0);
			var prevStripLastFace = new FaceInfo(0, 0, 0);
			int stripCount = allStrips.Count;

			Debug.Assert(stripCount > 0);

			// we infer the cw/ccw ordering depending on the number of indices
			// this is screwed up by the fact that we insert -1s to denote changing strips
			// this is to account for that
			int accountForNegatives = 0;

			for (int i = 0; i < stripCount; ++i)
			{
				var strip = allStrips[i];
				var faces = strip.FaceInfos;
				int stripFaceCount = strip.FaceInfos.Count;

				Debug.Assert(stripFaceCount > 0);

				// handle the first face in the strip
				{
					var beginFace = faces[0];
					var firstFace = new FaceInfo(beginFace.V0, beginFace.V1, beginFace.V2);

					// if there is a second face, reorder vertices such that the
					// unique vertex is first
					if (stripFaceCount > 1)
					{
						int nUnique = Stripifier.GetUniqueVertexInB(faces[1], firstFace);

						if (nUnique == firstFace.V1)
						{
							var temp = firstFace.V0;
							firstFace.V0 = firstFace.V1;
							firstFace.V1 = temp;
						}
						else if (nUnique == firstFace.V2)
						{
							var temp = firstFace.V0;
							firstFace.V0 = firstFace.V2;
							firstFace.V2 = temp;
						}

						// if there is a third face, reorder vertices such that the
						// shared vertex is last
						if (stripFaceCount > 2)
						{
							if (Stripifier.IsDegenerate(faces[1]))
							{
								int pivot = faces[1].V1;

								if (firstFace.V1 == pivot)
								{
									var temp = firstFace.V1;
									firstFace.V1 = firstFace.V2;
									firstFace.V2 = temp;
								}
							}
							else
							{
								Stripifier.GetSharedVertices(faces[2], firstFace, out int nShared0, out int nShared1);
								
								if (nShared0 == firstFace.V1 && nShared1 == -1)
								{
									var temp = firstFace.V1;
									firstFace.V1 = firstFace.V2;
									firstFace.V2 = temp;
								}
							}
						}
					}

					if (i == 0 || !stitchStrips || restart)
					{
						if (!Stripifier.IsCW(beginFace, firstFace.V0, firstFace.V1))
						{
							stripIndices.Add(firstFace.V0);
						}
					}
					else
					{
						// double tap the first in the new strip
						stripIndices.Add(firstFace.V0);

						// check CW/CCW ordering
						var nextIsCW = Stripifier.NextIsCW(stripIndices.Count - accountForNegatives);
						var thisIsCW = Stripifier.IsCW(beginFace, firstFace.V0, firstFace.V1);

						if (nextIsCW != thisIsCW)
						{
							stripIndices.Add(firstFace.V0);
						}
					}

					stripIndices.Add(firstFace.V0);
					stripIndices.Add(firstFace.V1);
					stripIndices.Add(firstFace.V2);

					// update last face info
					lastFace = firstFace;
				}

				for (int j = 1; j < stripFaceCount; ++j)
				{
					int nUnique = Stripifier.GetUniqueVertexInB(lastFace, faces[j]);
					
					if (nUnique != -1)
					{
						stripIndices.Add(nUnique);

						// update last face info
						lastFace.V0 = lastFace.V1;
						lastFace.V1 = lastFace.V2;
						lastFace.V2 = nUnique;
					}
					else
					{
						// we've hit a degenerate
						stripIndices.Add(faces[j].V2);

						lastFace.V0 = faces[j].V0; // lastFace.V1;
						lastFace.V1 = faces[j].V1; // lastFace.V2;
						lastFace.V2 = faces[j].V2; // lastFace.V1;
					}
				}

				// double tap between strips.
				if (stitchStrips && !restart)
				{
					if (i != stripCount - 1)
					{
						stripIndices.Add(lastFace.V2);
					}
				}
				else if (restart)
				{
					stripIndices.Add(restartValue);
				}
				else
				{
					// -1 index indicates next strip
					stripIndices.Add(-1);
					++accountForNegatives;
					++numSeparateStrips;
				}

				// Update last face info
				lastFace.V0 = lastFace.V1;
				lastFace.V1 = lastFace.V2;
				lastFace.V2 = lastFace.V2;
			}

			if (stitchStrips || restart)
			{
				numSeparateStrips = 1;
			}
		}
	}
}
