"""
Runs every controlled experiment with the Windows build (headless, fixed time step) and merges the
results into docs/week07/data/. Each condition uses the same seeds, so generation 1 starts from the
same genomes and spawn positions in every condition.

  python tools/run_experiments.py            (build: Tools > AI Games Week 3 > Build Windows Player)
"""
import shutil
import subprocess
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

EXE = Path("Builds/StayOnPlatform/StayOnPlatform.exe")
OUT = Path("docs/week07/data")
SEEDS = list(range(2026, 2036))
GENERATIONS = 30

# id: (mutation, fitness weight, inherit genes)
CONDITIONS = {
    "mut0.00": (0.00, 0.55, 1),
    "mut0.10": (0.10, 0.55, 1),
    "mut0.30": (0.30, 0.55, 1),
    "starter": (0.10, 0.55, 0),   # TODO D not solved: elites + random genomes, no inheritance
    "w0.00": (0.10, 0.00, 1),     # fitness = survival time only
    "w5.00": (0.10, 5.00, 1),     # distance reward strongly increased
}
LOG_RUN = ("mut0.10", 2026)  # its per-generation log lines are kept as evidence


def run(cond, seed, tmp, tag=""):
    mutation, weight, inherit = CONDITIONS[cond]
    name = f"{cond}_{seed}{tag}"
    args = [str(EXE), "-batchmode", "-nographics", "-experiment", str(tmp / f"{name}.csv"),
            "-condition", cond, "-seed", str(seed), "-mutation", str(mutation),
            "-fitnessWeight", str(weight), "-inherit", str(inherit), "-generations", str(GENERATIONS),
            "-logFile", str(tmp / f"{name}.log")]
    if cond == "starter":
        args += ["-botsCsv", str(tmp / f"{name}_bots.csv")]
    subprocess.run(args, check=True, timeout=600)
    return name


def merge(files, target):
    lines = []
    for i, f in enumerate(files):
        rows = f.read_text(encoding="utf-8").splitlines()
        lines += rows if i == 0 else rows[1:]  # keep one header
    target.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    if not EXE.exists():
        raise SystemExit(f"build not found: {EXE}")
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = Path(tempfile.mkdtemp(prefix="stayonplatform_"))
    jobs = [(c, s) for c in CONDITIONS for s in SEEDS]
    with ThreadPoolExecutor(max_workers=3) as pool:
        for name in pool.map(lambda j: run(*j, tmp), jobs):
            print("done", name, flush=True)

    merge([tmp / f"{c}_{s}.csv" for c, s in jobs], OUT / "generations.csv")
    merge([tmp / f"starter_{s}_bots.csv" for s in SEEDS], OUT / "bots_starter.csv")

    cond, seed = LOG_RUN
    log = (tmp / f"{cond}_{seed}.log").read_text(encoding="utf-8", errors="replace")
    (OUT / f"log_{cond}_seed{seed}.txt").write_text(
        "\n".join(l for l in log.splitlines() if l.startswith("Generation ")) + "\n", encoding="utf-8")

    # Same seed, second run: the rows must be identical for the experiment to be reproducible.
    run(cond, seed, tmp, "_repeat")
    same = (tmp / f"{cond}_{seed}.csv").read_bytes() == (tmp / f"{cond}_{seed}_repeat.csv").read_bytes()
    (OUT / "repeat_check.txt").write_text(
        f"{cond} seed {seed} run twice, {GENERATIONS} generations: "
        f"{'identical CSV rows' if same else 'DIFFERENT CSV rows'}\n", encoding="utf-8")
    print("repeat identical:", same)
    shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
