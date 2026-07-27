"use client";

import { useMemo, useState } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { productCategories } from "@/data/products";
import { ProductCard } from "@/components/products/ProductCard";
import { EmptyState } from "@/components/ui/EmptyState";
import { FormField, inputClassName } from "@/components/ui/FormField";

export default function ProductsPage() {
  const products = useProductsStore((s) => s.products);
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("all");
  const [availability, setAvailability] = useState("all");
  const [sort, setSort] = useState("featured");

  const filtered = useMemo(() => {
    let list = [...products];

    if (query.trim()) {
      const q = query.toLowerCase();
      list = list.filter((p) => p.name.toLowerCase().includes(q));
    }
    if (category !== "all") {
      list = list.filter((p) => p.category === category);
    }
    if (availability === "available") {
      list = list.filter((p) => p.isAvailable);
    }
    if (availability === "unavailable") {
      list = list.filter((p) => !p.isAvailable);
    }
    if (sort === "price-asc") {
      list.sort((a, b) => a.price - b.price);
    } else if (sort === "price-desc") {
      list.sort((a, b) => b.price - a.price);
    } else {
      list.sort((a, b) => Number(b.isFeatured) - Number(a.isFeatured));
    }
    return list;
  }, [products, query, category, availability, sort]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h1 className="font-serif text-4xl text-esotera-white">Produtos</h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Catálogo do protótipo Esotera.
      </p>

      <div className="mt-8 grid gap-4 rounded-lg border border-esotera-graphite p-4 sm:grid-cols-2 lg:grid-cols-4">
        <FormField label="Buscar por nome" id="search">
          <input
            id="search"
            className={inputClassName}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Ex.: Waite"
          />
        </FormField>
        <FormField label="Categoria" id="category">
          <select
            id="category"
            className={inputClassName}
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          >
            <option value="all">Todas</option>
            {productCategories.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Disponibilidade" id="availability">
          <select
            id="availability"
            className={inputClassName}
            value={availability}
            onChange={(e) => setAvailability(e.target.value)}
          >
            <option value="all">Todos</option>
            <option value="available">Disponíveis</option>
            <option value="unavailable">Indisponíveis</option>
          </select>
        </FormField>
        <FormField label="Ordenação" id="sort">
          <select
            id="sort"
            className={inputClassName}
            value={sort}
            onChange={(e) => setSort(e.target.value)}
          >
            <option value="featured">Destaques</option>
            <option value="price-asc">Menor preço</option>
            <option value="price-desc">Maior preço</option>
          </select>
        </FormField>
      </div>

      {filtered.length === 0 ? (
        <div className="mt-10">
          <EmptyState
            title="Nenhum produto encontrado"
            description="Ajuste os filtros ou a busca para ver outros itens."
          />
        </div>
      ) : (
        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((product) => (
            <ProductCard key={product.id} product={product} />
          ))}
        </div>
      )}
    </div>
  );
}
