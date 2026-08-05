#!/usr/bin/env python3
"""
Generate a synthetic vector dataset (BigANN .fbin format) plus an exact
ground-truth file for recall evaluation, used by the spherical-vs-product
quantizer comparison harness.

Outputs (into --outdir):
  wikipedia_base_<nbase>.fbin   : nbase x dim float32 base vectors
  wikipedia_query.fbin          : nquery x dim float32 query vectors
  wikipedia_truth_<nbase>       : exact top-gtK neighbors per query (BigANN GT format)

fbin format:  int32 count, int32 dim, then count*dim float32.
GT format  :  int32 nquery, int32 gtK,
              then nquery*gtK int32 neighbor ids,
              then nquery*gtK float32 similarity scores.
Ground truth is computed by exact dot-product (DistanceFunction.DotProduct):
nearest neighbors = highest dot product (descending).
"""
import argparse
import numpy as np


def write_fbin(path, arr):
    arr = np.ascontiguousarray(arr, dtype=np.float32)
    n, d = arr.shape
    with open(path, "wb") as f:
        np.array([n, d], dtype=np.int32).tofile(f)
        arr.tofile(f)


def write_ground_truth(path, ids, scores):
    nq, k = ids.shape
    with open(path, "wb") as f:
        np.array([nq, k], dtype=np.int32).tofile(f)
        np.ascontiguousarray(ids, dtype=np.int32).tofile(f)
        np.ascontiguousarray(scores, dtype=np.float32).tofile(f)


def gen_vectors(dist, n, dim, rng):
    if dist == "gaussian":
        return rng.standard_normal((n, dim)).astype(np.float32)
    if dist == "clustered":
        n_clusters = 50
        centers = rng.standard_normal((n_clusters, dim)).astype(np.float32) * 5.0
        assign = rng.integers(0, n_clusters, size=n)
        noise = rng.standard_normal((n, dim)).astype(np.float32) * 0.5
        return centers[assign] + noise
    if dist == "uniform":
        return rng.random((n, dim), dtype=np.float32) * 2.0 - 1.0
    raise ValueError(f"unknown dist {dist}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--outdir", required=True)
    ap.add_argument("--dist", default="gaussian", choices=["gaussian", "clustered", "uniform"])
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--nbase", type=int, default=5000)
    ap.add_argument("--nquery", type=int, default=50)
    ap.add_argument("--dim", type=int, default=768)
    ap.add_argument("--gtk", type=int, default=100)
    args = ap.parse_args()

    rng = np.random.default_rng(args.seed)
    base = gen_vectors(args.dist, args.nbase, args.dim, rng)
    query = gen_vectors(args.dist, args.nquery, args.dim, rng)

    # Exact top-gtK by dot product (DotProduct distance function): highest dot product first.
    # base: (nbase, dim), query: (nquery, dim) -> sims: (nquery, nbase)
    sims = query @ base.T
    k = min(args.gtk, args.nbase)
    # argpartition for top-k, then sort those k descending.
    part = np.argpartition(-sims, kth=k - 1, axis=1)[:, :k]
    part_scores = np.take_along_axis(sims, part, axis=1)
    order = np.argsort(-part_scores, axis=1)
    gt_ids = np.take_along_axis(part, order, axis=1).astype(np.int32)
    gt_scores = np.take_along_axis(part_scores, order, axis=1).astype(np.float32)

    base_path = f"{args.outdir}/wikipedia_base_{args.nbase}.fbin"
    query_path = f"{args.outdir}/wikipedia_query.fbin"
    gt_path = f"{args.outdir}/wikipedia_truth_{args.nbase}"

    write_fbin(base_path, base)
    write_fbin(query_path, query)
    write_ground_truth(gt_path, gt_ids, gt_scores)

    print(f"dist={args.dist} seed={args.seed} nbase={args.nbase} nquery={args.nquery} "
          f"dim={args.dim} gtK={k}")
    print(f"wrote {base_path}")
    print(f"wrote {query_path}")
    print(f"wrote {gt_path}")


if __name__ == "__main__":
    main()
