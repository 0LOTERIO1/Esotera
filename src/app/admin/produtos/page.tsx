"use client";

import { useState } from "react";
import { useProductsStore } from "@/stores/productsStore";
import { Price } from "@/components/ui/Price";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { useToastStore } from "@/stores/toastStore";
import type { Product } from "@/types";

const emptyForm = {
  name: "",
  slug: "",
  shortDescription: "",
  description: "",
  price: "39.90",
  category: "Tarôs",
  features: "",
  isFeatured: false,
  isAvailable: true,
};

export default function AdminProductsPage() {
  const products = useProductsStore((s) => s.products);
  const upsert = useProductsStore((s) => s.upsert);
  const setAvailability = useProductsStore((s) => s.setAvailability);
  const push = useToastStore((s) => s.push);
  const [editing, setEditing] = useState<Product | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [open, setOpen] = useState(false);

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setOpen(true);
  }

  function openEdit(product: Product) {
    setEditing(product);
    setForm({
      name: product.name,
      slug: product.slug,
      shortDescription: product.shortDescription,
      description: product.description,
      price: String(product.price),
      category: product.category,
      features: product.features.join(", "),
      isFeatured: product.isFeatured,
      isAvailable: product.isAvailable,
    });
    setOpen(true);
  }

  function save() {
    const price = Number(form.price.replace(",", "."));
    if (!form.name.trim() || !form.slug.trim() || Number.isNaN(price)) {
      push("error", "Preencha nome, slug e preço válidos.");
      return;
    }
    upsert({
      id: editing?.id,
      name: form.name.trim(),
      slug: form.slug.trim(),
      shortDescription: form.shortDescription.trim() || "Produto de demonstração.",
      description: form.description.trim() || "Descrição de demonstração.",
      price,
      category: form.category,
          images: editing?.images ?? ["/images/products/waite-tradicional.png"],
      features: form.features
        .split(",")
        .map((f) => f.trim())
        .filter(Boolean),
      isFeatured: form.isFeatured,
      isAvailable: form.isAvailable,
      isDemo: true,
    });
    push("success", editing ? "Produto atualizado." : "Produto cadastrado.");
    setOpen(false);
  }

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-serif text-3xl text-esotera-white">Produtos</h1>
        <Button type="button" onClick={openCreate}>
          Novo produto
        </Button>
      </div>

      <ul className="mt-6 space-y-3">
        {products.map((product) => (
          <li
            key={product.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-esotera-graphite p-4"
          >
            <div>
              <p className="text-esotera-beige">{product.name}</p>
              <p className="text-xs text-esotera-muted">
                {product.category} · <Price value={product.price} /> ·{" "}
                {product.isAvailable ? "Disponível" : "Indisponível"}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="secondary" onClick={() => openEdit(product)}>
                Editar
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  setAvailability(product.id, !product.isAvailable);
                  push(
                    "success",
                    product.isAvailable
                      ? "Marcado como indisponível."
                      : "Marcado como disponível.",
                  );
                }}
              >
                {product.isAvailable ? "Indisponibilizar" : "Disponibilizar"}
              </Button>
            </div>
          </li>
        ))}
      </ul>

      {open ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
          <div
            role="dialog"
            aria-modal
            aria-labelledby="product-form-title"
            className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg border border-esotera-graphite bg-esotera-navy p-6"
          >
            <h2 id="product-form-title" className="font-serif text-xl text-esotera-white">
              {editing ? "Editar produto" : "Cadastrar produto"}
            </h2>
            <div className="mt-4 grid gap-3">
              <FormField label="Nome" id="p-name" required>
                <input
                  id="p-name"
                  className={inputClassName}
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </FormField>
              <FormField label="Slug" id="p-slug" required>
                <input
                  id="p-slug"
                  className={inputClassName}
                  value={form.slug}
                  onChange={(e) => setForm({ ...form, slug: e.target.value })}
                />
              </FormField>
              <FormField label="Preço" id="p-price" required>
                <input
                  id="p-price"
                  className={inputClassName}
                  value={form.price}
                  onChange={(e) => setForm({ ...form, price: e.target.value })}
                />
              </FormField>
              <FormField label="Categoria" id="p-category">
                <input
                  id="p-category"
                  className={inputClassName}
                  value={form.category}
                  onChange={(e) => setForm({ ...form, category: e.target.value })}
                />
              </FormField>
              <FormField label="Descrição curta" id="p-short">
                <input
                  id="p-short"
                  className={inputClassName}
                  value={form.shortDescription}
                  onChange={(e) =>
                    setForm({ ...form, shortDescription: e.target.value })
                  }
                />
              </FormField>
              <FormField label="Descrição" id="p-desc">
                <textarea
                  id="p-desc"
                  className={inputClassName}
                  rows={3}
                  value={form.description}
                  onChange={(e) =>
                    setForm({ ...form, description: e.target.value })
                  }
                />
              </FormField>
              <FormField label="Características (separadas por vírgula)" id="p-feat">
                <input
                  id="p-feat"
                  className={inputClassName}
                  value={form.features}
                  onChange={(e) => setForm({ ...form, features: e.target.value })}
                />
              </FormField>
              <label className="flex items-center gap-2 text-sm text-esotera-muted">
                <input
                  type="checkbox"
                  checked={form.isAvailable}
                  onChange={(e) =>
                    setForm({ ...form, isAvailable: e.target.checked })
                  }
                />
                Disponível
              </label>
              <label className="flex items-center gap-2 text-sm text-esotera-muted">
                <input
                  type="checkbox"
                  checked={form.isFeatured}
                  onChange={(e) =>
                    setForm({ ...form, isFeatured: e.target.checked })
                  }
                />
                Destaque
              </label>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <Button type="button" variant="secondary" onClick={() => setOpen(false)}>
                Cancelar
              </Button>
              <Button type="button" onClick={save}>
                Salvar
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
