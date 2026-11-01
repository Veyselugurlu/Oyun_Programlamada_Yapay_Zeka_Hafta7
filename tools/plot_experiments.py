"""
Draws the report figures from docs/week07/data/generations.csv and bots_starter.csv
(written by tools/run_experiments.py) and saves docs/week07/data/summary.json.

  python tools/plot_experiments.py
"""
import csv
import json
import statistics as st
from collections import defaultdict
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

DATA = Path("docs/week07/data")
OUT = Path("docs/week07/images")
OUT.mkdir(parents=True, exist_ok=True)

INK, MUTED, GRID = "#2b2b2a", "#6b6a64", "#e4e3de"
plt.rcParams.update({
    "font.size": 10, "axes.edgecolor": MUTED, "axes.labelcolor": INK, "xtick.color": MUTED,
    "ytick.color": MUTED, "axes.spines.top": False, "axes.spines.right": False,
    "axes.grid": True, "axes.axisbelow": True, "grid.color": GRID, "grid.linewidth": 0.8, "axes.titlesize": 11,
})

# Strategy of a genome [g0, g1] (0 forward, 1 left, 2 right); same hues as GenomeColors.cs
STRATEGIES = [
    ("Kenarda döner [0, 1|2]", "#1baf7a", ["n01", "n02"]),
    ("Hep ileri [0, 0]", "#e34948", ["n00"]),
    ("Yerinde döner [1|2, 1|2]", "#eda100", ["n11", "n12", "n21", "n22"]),
    ("Döner, kenarda ileri [1|2, 0]", "#6250d6", ["n10", "n20"]),
]
GENOME_STRATEGY = {"00": 1, "01": 0, "02": 0, "10": 3, "20": 3, "11": 2, "12": 2, "21": 2, "22": 2}

MUTATION = [("mut0.00", "Mutasyon %0", "#2a78d6", "-"), ("mut0.10", "Mutasyon %10", "#eb6834", "-"),
            ("mut0.30", "Mutasyon %30", "#1baf7a", "-"), ("starter", "Starter (kalıtım yok)", "#8a8981", "--")]
WEIGHTS = [("w0.00", "w = 0 (yalnız yaşam süresi)", "#2a78d6"), ("mut0.10", "w = 0.55 (ders değeri)", "#eb6834"),
           ("w5.00", "w = 5 (mesafe ağırlıklı)", "#1baf7a")]
FIELDS = ["mean_fitness", "best_fitness", "survivors", "mean_time_alive", "mean_distance"]


def load():
    data = defaultdict(lambda: defaultdict(lambda: defaultdict(list)))  # cond -> field -> gen -> [per seed]
    best_dna = defaultdict(lambda: defaultdict(list))
    for r in csv.DictReader((DATA / "generations.csv").open(encoding="utf-8")):
        c, g = r["condition"], int(r["generation"])
        for f in FIELDS:
            data[c][f][g].append(float(r[f]))
        total = sum(int(r[f"n{a}{b}"]) for a in range(3) for b in range(3))
        for k, (_, _, cols) in enumerate(STRATEGIES):
            data[c][f"share{k}"][g].append(sum(int(r[col]) for col in cols) / total)
        best_dna[c][g].append(r["best_dna"])
    return data, best_dna


def series(data, cond, field):
    gens = sorted(data[cond][field])
    v = np.array([data[cond][field][g] for g in gens])
    return np.array(gens), v.mean(axis=1), v.std(axis=1)


def line(ax, data, cond, field, label, color, style="-", end_label=True):
    g, m, s = series(data, cond, field)
    ax.plot(g, m, color=color, lw=2, ls=style, label=label)
    ax.fill_between(g, m - s, m + s, color=color, alpha=0.12, lw=0)
    if end_label:
        ax.annotate(f"{m[-1]:.2f}", (g[-1], m[-1]), xytext=(4, 0), textcoords="offset points",
                    va="center", fontsize=8, color=INK)


def fig_mutation(data, n_seeds):
    fig, axes = plt.subplots(1, 3, figsize=(15, 4.6))
    for cond, label, color, style in MUTATION:
        line(axes[0], data, cond, "mean_fitness", label, color, style)
        line(axes[1], data, cond, "best_fitness", label, color, style)
        line(axes[2], data, cond, "survivors", label, color, style, end_label=False)
    axes[0].set_title("Ortalama fitness")
    axes[1].set_title("En iyi fitness")
    axes[2].set_title("Nesil sonunda hayatta kalan (24 bireyden)")
    axes[2].set_ylim(0, 25)
    for ax in axes:
        ax.set_xlabel("Nesil")
        ax.set_xlim(1, 31.5)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="lower center", ncol=4, frameon=False)
    fig.suptitle(f"Üç mutasyon oranı ve starter karşılaştırması ({n_seeds} seed; çizgi: ortalama, gölge: ±1 std)")
    fig.tight_layout(rect=(0, 0.07, 1, 1))
    fig.savefig(OUT / "mutation_rates.png", dpi=140)
    plt.close(fig)


def stacked(ax, data, cond, title):
    g, _, _ = series(data, cond, "share0")
    shares = [series(data, cond, f"share{k}")[1] for k in range(len(STRATEGIES))]
    ax.stackplot(g, shares, colors=[c for _, c, _ in STRATEGIES], edgecolor="white", linewidth=1)
    ax.set_title(title)
    ax.set_xlim(1, 30)
    ax.set_ylim(0, 1)
    ax.set_ylabel("Popülasyon payı")
    ax.grid(False)
    ax.text(29.4, 0.5, f"%{100 * shares[0][-1]:.0f}", ha="right", va="center", color="white",
            fontsize=10, fontweight="bold")


def fig_strategies(data):
    conds = [("starter", "Starter (kalıtım yok)"), ("mut0.00", "Mutasyon %0"), ("mut0.10", "Mutasyon %10"),
             ("mut0.30", "Mutasyon %30"), ("w0.00", "w = 0: yalnız yaşam süresi"), ("w5.00", "w = 5: mesafe ağırlıklı")]
    fig, axes = plt.subplots(2, 3, figsize=(15, 7.4), sharex=True, sharey=True)
    for ax, (cond, title) in zip(axes.flat, conds):
        stacked(ax, data, cond, title)
    for ax in axes[1]:
        ax.set_xlabel("Nesil")
    handles = [plt.Rectangle((0, 0), 1, 1, color=c) for _, c, _ in STRATEGIES]
    fig.legend(handles, [n for n, _, _ in STRATEGIES], loc="lower center", ncol=4, frameon=False)
    fig.suptitle("Popülasyondaki stratejilerin payı (seed ortalaması; beyaz sayı: 30. nesilde 'kenarda döner' payı)")
    fig.tight_layout(rect=(0, 0.05, 1, 1))
    fig.savefig(OUT / "strategy_share.png", dpi=140)
    plt.close(fig)


def landscape():
    """Behaviour of each of the 3^2 = 9 genomes, from the starter runs (random genomes every generation)."""
    per = defaultdict(lambda: {"time": [], "dist": [], "alive": []})
    for r in csv.DictReader((DATA / "bots_starter.csv").open(encoding="utf-8")):
        key = r["g0"] + r["g1"]
        per[key]["time"].append(float(r["time_alive"]))
        per[key]["dist"].append(float(r["distance"]))
        per[key]["alive"].append(int(r["alive"]))
    table = {}
    for key in sorted(per):
        t, d = np.array(per[key]["time"]), np.array(per[key]["dist"])
        table[f"[{key[0]}, {key[1]}]"] = {
            "n": len(t), "time_alive": round(t.mean(), 3), "distance": round(d.mean(), 3),
            "survival_rate": round(float(np.mean(per[key]["alive"])), 3),
            "fitness_w0": round(t.mean(), 3), "fitness_w055": round(t.mean() + 0.55 * d.mean(), 3),
            "fitness_w5": round(t.mean() + 5 * d.mean(), 3), "strategy": GENOME_STRATEGY[key],
        }
    return table


def fig_landscape(table):
    keys = list(table)
    colors = [STRATEGIES[table[k]["strategy"]][1] for k in keys]
    fig, axes = plt.subplots(1, 3, figsize=(15, 4.4))
    for ax, field, title in [(axes[0], "time_alive", "Ortalama yaşam süresi (s, en fazla 8)"),
                             (axes[1], "distance", "Ortalama yatay mesafe (birim)"),
                             (axes[2], "fitness_w055", "Fitness = süre + 0.55 × mesafe")]:
        vals = [table[k][field] for k in keys]
        bars = ax.bar(keys, vals, color=colors, width=0.7, edgecolor="white", linewidth=2)
        for b, v in zip(bars, vals):
            ax.annotate(f"{v:.1f}", (b.get_x() + b.get_width() / 2, v), xytext=(0, 2),
                        textcoords="offset points", ha="center", fontsize=8, color=INK)
        ax.set_title(title)
        ax.set_xlabel("DNA [zemin, kenar]")
        ax.grid(axis="x", visible=False)
    handles = [plt.Rectangle((0, 0), 1, 1, color=c) for _, c, _ in STRATEGIES]
    fig.legend(handles, [n for n, _, _ in STRATEGIES], loc="lower center", ncol=4, frameon=False)
    n = sum(v["n"] for v in table.values())
    fig.suptitle(f"9 olası DNA'nın davranışı ({n} bot denemesi, starter koşusundaki rastgele genomlar)")
    fig.tight_layout(rect=(0, 0.06, 1, 1))
    fig.savefig(OUT / "genome_landscape.png", dpi=140)
    plt.close(fig)


def fig_weights(data):
    fig, axes = plt.subplots(1, 2, figsize=(14, 4.6))
    for cond, label, color in WEIGHTS:
        # w = 0.55 and w = 5 end almost on the same value, so the legend names the lines
        line(axes[0], data, cond, "mean_distance", label, color, end_label=False)
        line(axes[1], data, cond, "mean_time_alive", label, color, end_label=False)
    axes[0].set_title("Ortalama yatay mesafe (birim)")
    axes[1].set_title("Ortalama yaşam süresi (s)")
    for ax in axes:
        ax.set_xlabel("Nesil")
        ax.set_xlim(1, 31.5)
    axes[0].legend(loc="lower right", frameon=False)
    fig.suptitle("Fitness tasarımı: hareket ağırlığı w değişince evrilen davranış (mutasyon %10)")
    fig.tight_layout()
    fig.savefig(OUT / "fitness_weights.png", dpi=140)
    plt.close(fig)


def summarize(data, best_dna, table, n_seeds):
    summary = {"seeds": n_seeds, "conditions": {}, "genomes": table}
    for cond in data:
        entry = {}
        for gen in (1, 2, 5, 10, 30):
            for f in FIELDS + [f"share{k}" for k in range(len(STRATEGIES))]:
                v = data[cond][f][gen]
                entry[f"{f}_g{gen}"] = [round(st.mean(v), 3), round(st.stdev(v), 3)]
        # first generation in which edge turners make up at least 75 % of the population, per seed
        firsts = []
        for i in range(n_seeds):
            hit = [g for g in sorted(data[cond]["share0"]) if data[cond]["share0"][g][i] >= 0.75]
            firsts.append(hit[0] if hit else None)
        entry["first_gen_edge_turner_75"] = firsts
        dnas = best_dna[cond][30]
        entry["best_dna_g30"] = {d: dnas.count(d) for d in sorted(set(dnas))}
        summary["conditions"][cond] = entry
    (DATA / "summary.json").write_text(json.dumps(summary, indent=1, ensure_ascii=False), encoding="utf-8")


def main():
    data, best_dna = load()
    n_seeds = len(data["mut0.10"]["mean_fitness"][1])
    table = landscape()
    fig_mutation(data, n_seeds)
    fig_strategies(data)
    fig_landscape(table)
    fig_weights(data)
    summarize(data, best_dna, table, n_seeds)
    print("figures ->", OUT)


if __name__ == "__main__":
    main()
