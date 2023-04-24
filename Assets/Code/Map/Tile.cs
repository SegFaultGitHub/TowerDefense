using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Code.Map {
    public class Tile : MonoBehaviour {
        private LTDescr FogTween, Tween;
        public Dictionary<string, Tile> Neighbours { get; private set; }
        [field: SerializeField] public Vector3Int GridPosition { get; set; }
        [field: SerializeField] public Vector3 Position { get; set; }
        [field: SerializeField] public bool Walkable { get; set; }
        public float Height { get; set; }
        [field: SerializeField] public bool Occupied { get; set; }
        public GameObject Objects { get; private set; }

        protected virtual void Awake() {
            this.Objects = this.transform.Find("Model/Objects").gameObject;
            this.Neighbours = new Dictionary<string, Tile>();
        }

        public void ArrangeObjects() {
            Vector3 scale = new(1, 1 / this.Height, 1);
            this.Objects.transform.localScale = scale;
        }

        public List<Tile> WalkableNeighbours(float? stepOffset) {
            return (
                from tile in this.Neighbours.Values
                let heightDifference = stepOffset == null || Mathf.Abs(tile.Height - this.Height) < stepOffset
                where tile.Walkable && heightDifference && !tile.Occupied
                select tile).ToList();
        }

        public Path PathFind(Tile to, float stepOffset) {
            return PathFinder.PathFind(this, to, stepOffset);
        }

        public int DistanceFrom(Tile other) {
            return (int)((Math.Abs(this.GridPosition.x - other.GridPosition.x)
                          + Math.Abs(this.GridPosition.y - other.GridPosition.y)
                          + Math.Abs(this.GridPosition.z - other.GridPosition.z))
                         / 2f);
        }
    }
}
