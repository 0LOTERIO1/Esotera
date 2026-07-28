"use client";

import { useMemo, useState } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { productCategories } from "@/data/products";
import { ProductCard } from "@/components/products/ProductCard";
import { ProductGrid } from "@/components/products/ProductGrid";
import { EmptyState } from "@/components/ui/EmptyState";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { Button } from "@/components/ui/Button";
import { LoadingState } from "@/components/ui/LoadingState";
import { useStoreHydration } from "@/hooks/useStoreHydration";

export default function ProductsPage() {
  const hydrated = useStoreHydration();
  const products = useProductsStore((s) => s.products);
  const loading = useProductsStore((s) => s.loading);
  const error = useProductsStore((s) => s.error);
  const refresh = useProductsStore((s) => s.refresh);
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("all");
  const [availability, setAvailability] = useState("all");
  const [sort, setSort] = useState("featured");
  const [filtersOpen, setFiltersOpen] = useState(false);

  const categories = useMemo(() => {
    const fromApi = Array.from(new Set(products.map((p) => p.category))).sort();
    return fromApi.length ? fromApi : [...productCategories];
  }, [products]);

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
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
      <h1 className="font-serif text-3xl text-esotera-secondary sm:text-4xl">
        Produtos
      </h1>
      <p className="mt-2 text-sm text-esotera-muted">
        Catálogo Esotera — escolha e compre com praticidade.
      </p>

      <div className="mt-5 md:hidden">
        <Button
          type="button"
          variant="secondary"
          className="w-full"
          onClick={() => setFiltersOpen((v) => !v)}
        >
          {filtersOpen ? "Ocultar filtros" : "Filtros e ordenação"}
        </Button>
      </div>

      <div
        className={`mt-4 grid gap-3 rounded-lg border border-esotera-border bg-esotera-surface p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-4 ${
          filtersOpen ? "grid" : "hidden md:grid"
        }`}
      >
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
            {categories.map((c) => (
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

      {!hydrated || loading ? (
        <div className="mt-8">
          <LoadingState label="Carregando produtos…" />
        </div>
      ) : error ? (
        <div className="mt-8">
          <EmptyState
            title="Catálogo indisponível"
            description={error}
            action={
              <Button type="button" onClick={() => void refresh()}>
                Tentar novamente
              </Button>
            }
          />
        </div>
      ) : filtered.length === 0 ? (
        <div className="mt-8">
          <EmptyState
            title="Nenhum produto encontrado"
            description="Ajuste os filtros ou a busca para ver outros itens."
          />
        </div>
      ) : (
        <div className="mt-6">
          <ProductGrid>
            {filtered.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </ProductGrid>
        </div>
      )}
    </div>
  );
}
