"""
Fetch a REAL slice of the BigANN Wikipedia-Cohere dataset (35M x 768, inner-product)
and produce the files the VectorIndexScenarioSuite wiki-cohere scenario reads:

  wikipedia_base_<nbase>.fbin   : first <nbase> real base vectors (header rewritten)
  wikipedia_query.fbin          : first <nquery> real query vectors
  wikipedia_truth_<nbase>       : exact top-<gtk> neighbors per query (BigANN GT format),
                                  computed locally by exact dot product over the crop.

Source (see big-ann-benchmarks datasets.py, class WikipediaDataset):
  base : https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_base.bin
  query: https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_query.bin
  distance = inner product (dot product); dtype float32; d = 768.

We use HTTP range GETs so only ~(8 + d*n*4) bytes are downloaded, not the full 107 GB.
"""
import argparse
import os
import struct
import urllib.request

import numpy as np

BASE_URL = "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_base.bin"
QUERY_URL = "https://comp21storage.z5.web.core.windows.net/wiki-cohere-35M/wikipedia_query.bin"
DIM = 768
DTYPE = np.float32
ITEMSIZE = 4


def range_get(url, nbytes):
    """Download the first nbytes bytes of url via an HTTP range request."""
    req = urllib.request.Request(url, headers={"Range": f"bytes=0-{nbytes - 1}"})
    with urllib.request.urlopen(req, timeout=120) as resp:
        data = resp.read()
    if len(data) != nbytes:
        raise RuntimeError(f"expected {nbytes} bytes, got {len(data)} from {url}")
    return data


def read_fbin_bytes(raw, expected_count, expected_dim):
    count = struct.unpack_from("<i", raw, 0)[0]
    dim = struct.unpack_from("<i", raw, 4)[0]
    if dim != expected_dim:
        raise RuntimeError(f"dim mismatch: header dim={dim}, expected {expected_dim}")
    if count < expected_count:
        raise RuntimeError(f"source only has {count} vectors, need {expected_count}")
    arr = np.frombuffer(raw, dtype=DTYPE, count=expected_count * dim, offset=8)
    return arr.reshape(expected_count, dim).copy()


def write_fbin(path, arr):
    n, d = arr.shape
    with open(path, "wb") as f:
        f.write(struct.pack("<i", n))
        f.write(struct.pack("<i", d))
        f.write(arr.astype(DTYPE).tobytes())


def write_ground_truth(path, ids, scores):
    nq, k = ids.shape
    with open(path, "wb") as f:
        f.write(struct.pack("<i", nq))
        f.write(struct.pack("<i", k))
        f.write(ids.astype(np.int32).tobytes())
        f.write(scores.astype(np.float32).tobytes())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--outdir", required=True)
    ap.add_argument("--nbase", type=int, default=5000)
    ap.add_argument("--nquery", type=int, default=50)
    ap.add_argument("--gtk", type=int, default=100)
    ap.add_argument("--reuse-base", action="store_true",
                    help="Reuse an existing wikipedia_base_<nbase>.fbin if present with a matching "
                         "header, instead of re-downloading (the crop is deterministic).")
    args = ap.parse_args()

    os.makedirs(args.outdir, exist_ok=True)

    base_bytes = 8 + DIM * args.nbase * ITEMSIZE
    query_bytes = 8 + DIM * args.nquery * ITEMSIZE

    base_path = os.path.join(args.outdir, f"wikipedia_base_{args.nbase}.fbin")
    reuse = False
    if args.reuse_base and os.path.exists(base_path):
        with open(base_path, "rb") as f:
            hdr = f.read(8)
        if len(hdr) == 8 and struct.unpack("<ii", hdr) == (args.nbase, DIM):
            reuse = True
    if reuse:
        print(f"Reusing existing base {base_path} ({args.nbase}x{DIM}) ...")
        with open(base_path, "rb") as f:
            base = read_fbin_bytes(f.read(base_bytes), args.nbase, DIM)
    else:
        print(f"Downloading {base_bytes/1e6:.1f} MB of real base vectors ...")
        base = read_fbin_bytes(range_get(BASE_URL, base_bytes), args.nbase, DIM)
    print(f"Downloading {query_bytes/1e6:.1f} MB of real query vectors ...")
    query = read_fbin_bytes(range_get(QUERY_URL, query_bytes), args.nquery, DIM)

    # Exact top-gtk by INNER PRODUCT (dataset metric == DotProduct in the scenario).
    k = min(args.gtk, args.nbase)
    sims = query @ base.T  # (nquery, nbase)
    part = np.argpartition(-sims, kth=k - 1, axis=1)[:, :k]
    part_scores = np.take_along_axis(sims, part, axis=1)
    order = np.argsort(-part_scores, axis=1)
    gt_ids = np.take_along_axis(part, order, axis=1)
    gt_scores = np.take_along_axis(part_scores, order, axis=1)

    query_path = os.path.join(args.outdir, "wikipedia_query.fbin")
    gt_path = os.path.join(args.outdir, f"wikipedia_truth_{args.nbase}")

    if not reuse:
        write_fbin(base_path, base)
    write_fbin(query_path, query)
    write_ground_truth(gt_path, gt_ids, gt_scores)

    print(f"REAL wiki-cohere: nbase={args.nbase} nquery={args.nquery} dim={DIM} gtk={k} "
          f"metric=innerproduct")
    print(f"wrote {base_path}")
    print(f"wrote {query_path}")
    print(f"wrote {gt_path}")


if __name__ == "__main__":
    main()
