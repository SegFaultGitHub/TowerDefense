using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Map {
    public struct Path {
        public Tile Destination;
        public List<Tile> Tiles;
        public bool Complete;
        public int RenewAt;
    }

    public static class PathFinder {
        public static Path PathFind(Tile from, Tile to, float stepOffset) {
            long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float _H(Tile tile) => (tile.Position - to.Position).sqrMagnitude;

            List<Tile> openSet = new() { from };
            Dictionary<Tile, Tile> cameFrom = new();
            Dictionary<Tile, float> gScore = new() { [from] = 0 };
            Dictionary<Tile, float> fScore = new() { [from] = _H(from) };

            Tile _CheapestNode(IEnumerable<Tile> tiles) {
                Tile result = null;
                float score = float.MaxValue;
                foreach (Tile tile in tiles) {
                    if (fScore.GetValueOrDefault(tile, float.MaxValue) >= score) continue;
                    score = fScore[tile];
                    result = tile;
                }
                return result;
            }
            Path _Path(Tile _to, bool complete = true) {
                if (_to == from)
                    return new Path {
                        Destination = to,
                        Tiles = new List<Tile>(),
                        Complete = true
                    };

                List<Tile> path = new() { _to };
                Tile current = _to;
                while (cameFrom.ContainsKey(current)) {
                    current = cameFrom[current];
                    path.Insert(0, current);
                }
                return new Path {
                    Destination = to,
                    Tiles = path,
                    Complete = complete,
                    RenewAt = (int)(path.Count * 0.75f)
                };
            }

            while (openSet.Count > 0) {
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start > 100)
                    return _Path(_CheapestNode(fScore.Keys), false);
                Tile current = _CheapestNode(openSet);
                if (current == to)
                    return _Path(current);

                openSet.Remove(current);
                foreach (Tile neighbour in current.WalkableNeighbours(stepOffset)) {
                    float tentativeGScore = gScore[current] + Mathf.Abs(current.Height - neighbour.Height);
                    if (tentativeGScore >= gScore.GetValueOrDefault(neighbour, float.MaxValue)) continue;
                    cameFrom[neighbour] = current;
                    gScore[neighbour] = tentativeGScore;
                    fScore[neighbour] = tentativeGScore + _H(neighbour);
                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }

            return _Path(_CheapestNode(fScore.Keys));
        }
    }
}
