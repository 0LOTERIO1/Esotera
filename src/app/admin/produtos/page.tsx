"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Price } from "@/components/ui/Price";
import { Button } from "@/components/ui/Button";
import { FormField, inputClassName } from "@/components/ui/FormField";
import { ProductThumbnail } from "@/components/products/ProductThumbnail";
import { ConfirmModal } from "@/components/ui/ConfirmModal";
import { ProductImageManager } from "@/components/admin/products/ProductImageManager";
import { useToastStore } from "@/stores/toastStore";
import { useProductsStore } from "@/stores/productsStore";
import {
  fileToCompressedDataUrl,
  MAX_IMAGE_BYTES,
  validateProductImage,
} from "@/utils/imageStorage";
import { isApiMode } from "@/config/dataMode";
import { getProductRepository } from "@/services/repositories";
import { productsApi } from "@/services/api/productsApi";
import type { Product, ProductImageMeta, ProductVariation } from "@/types";

const emptyForm = {
  name: "",
  slug: "",
  shortDescription: "",
  description: "",
  price: "",
  category: "Tarôs",
  categoryId: "",
  features: "",
  packageContents: "",
  variations: "",
  isFeatured: false,
  isAvailable: true,
};

type FormState = typeof emptyForm;
type ArchiveFilter = "active" | "archived" | "all";

function splitList(value: string): string[] {
  return value
    .split(/[,;\n]/)
    .map((f) => f.trim())
    .filter(Boolean);
}

/** Formato: Nome|preço|1ou0 (disponível). Uma variação por linha. */
function parseVariations(value: string, fallbackPrice: number): ProductVariation[] {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line, index) => {
      const parts = line.split("|").map((p) => p.trim());
      const name = parts[0] || `Opção ${index + 1}`;
      const price = Number(parts[1]?.replace(",", ".")) || fallbackPrice;
      const available = parts[2] === undefined ? true : parts[2] !== "0";
      return {
        id: `var-${index + 1}-${name.toLowerCase().replace(/\s+/g, "-")}`,
        name,
        price,
        isAvailable: available,
      };
    });
}

function formatVariations(variations?: ProductVariation[]): string {
  if (!variations?.length) return "";
  return variations
    .map((v) => `${v.name}|${v.price}|${v.isAvailable ? 1 : 0}`)
    .join("\n");
}

function slugify(name: string): string {
  return name
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 180);
}

export default function AdminProductsPage() {
  const push = useToastStore((s) => s.push);
  const catalogUpsert = useProductsStore((s) => s.upsert);
  const catalogSetAvailability = useProductsStore((s) => s.setAvailability);
  const catalogRefresh = useProductsStore((s) => s.refresh);
  const mockProducts = useProductsStore((s) => s.products);
  const apiMode = isApiMode();

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [availabilityFilter, setAvailabilityFilter] = useState<"all" | "yes" | "no">(
    "all",
  );
  const [archivedFilter, setArchivedFilter] = useState<ArchiveFilter>("active");
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);

  const [editing, setEditing] = useState<Product | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const [detailImages, setDetailImages] = useState<ProductImageMeta[]>([]);
  const [archiveTarget, setArchiveTarget] = useState<Product | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const repo = getProductRepository();
      if (apiMode && repo.listAdmin) {
        const list = await repo.listAdmin({
          search: search.trim() || undefined,
          categoryId: categoryFilter || undefined,
          isAvailable:
            availabilityFilter === "all"
              ? "all"
              : availabilityFilter === "yes",
          archived: archivedFilter,
        });
        setProducts(list);
      } else {
        await catalogRefresh();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Falha ao carregar produtos.");
      if (apiMode) setProducts([]);
    } finally {
      setLoading(false);
    }
  }, [
    apiMode,
    search,
    categoryFilter,
    availabilityFilter,
    archivedFilter,
    catalogRefresh,
  ]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    if (!apiMode) return;
    const timer = window.setTimeout(() => {
      void productsApi
        .listCategories()
        .then((list) => setCategories(list.map((c) => ({ id: c.id, name: c.name }))))
        .catch(() => setCategories([]));
    }, 0);
    return () => window.clearTimeout(timer);
  }, [apiMode]);

  const filteredMock = useMemo(() => {
    let list = [...mockProducts];
    if (search.trim()) {
      const q = search.trim().toLowerCase();
      list = list.filter(
        (p) =>
          p.name.toLowerCase().includes(q) || p.slug.toLowerCase().includes(q),
      );
    }
    if (categoryFilter) {
      list = list.filter((p) => p.category === categoryFilter);
    }
    if (availabilityFilter === "yes") list = list.filter((p) => p.isAvailable);
    if (availabilityFilter === "no") list = list.filter((p) => !p.isAvailable);
    if (archivedFilter === "archived") list = list.filter((p) => p.isArchived);
    if (archivedFilter === "active") list = list.filter((p) => !p.isArchived);
    return list;
  }, [mockProducts, search, categoryFilter, availabilityFilter, archivedFilter]);

  const displayList = apiMode ? products : filteredMock;

  function openCreate() {
    setEditing(null);
    setForm({
      ...emptyForm,
      categoryId: categories[0]?.id ?? "",
      category: categories[0]?.name ?? "Tarôs",
    });
    setImagePreview(null);
    setImageFile(null);
    setImageError(null);
    setFormErrors({});
    setDetailImages([]);
    setOpen(true);
  }

  async function openEdit(product: Product) {
    setFormErrors({});
    setImageError(null);
    setImageFile(null);
    setSaving(true);
    try {
      const repo = getProductRepository();
      const detail =
        apiMode && repo.getAdminDetail
          ? (await repo.getAdminDetail(product.id)) ?? product
          : product;
      setEditing(detail);
      setForm({
        name: detail.name,
        slug: detail.slug,
        shortDescription: detail.shortDescription,
        description: detail.description,
        price: String(detail.price),
        category: detail.category,
        categoryId: detail.categoryId ?? "",
        features: detail.features.join(", "),
        packageContents: (detail.packageContents ?? []).join(", "),
        variations: formatVariations(detail.variations),
        isFeatured: detail.isFeatured,
        isAvailable: detail.isAvailable,
      });
      setImagePreview(detail.images[0] ?? null);
      setDetailImages(detail.productImages ?? []);
      setOpen(true);
    } catch (err) {
      push("error", err instanceof Error ? err.message : "Falha ao abrir produto.");
    } finally {
      setSaving(false);
    }
  }

  async function onFileChange(file: File | null) {
    setImageError(null);
    if (!file) return;
    const validation = validateProductImage(file);
    if (validation) {
      setImageError(validation);
      setImageFile(null);
      return;
    }
    try {
      const dataUrl = await fileToCompressedDataUrl(file);
      setImagePreview(dataUrl);
      setImageFile(file);
    } catch (err) {
      setImageError(err instanceof Error ? err.message : "Falha ao processar imagem.");
      setImageFile(null);
    }
  }

  function validateForm(): boolean {
    const errors: Record<string, string> = {};
    if (!form.name.trim()) errors.name = "Informe o nome do produto.";
    const price = Number(form.price.replace(",", "."));
    if (!form.price.trim() || Number.isNaN(price) || price <= 0) {
      errors.price = "Informe um preço válido.";
    }
    if (apiMode && !form.categoryId) errors.category = "Selecione a categoria.";
    if (!apiMode && !form.category.trim()) errors.category = "Informe a categoria.";
    if (!form.shortDescription.trim()) {
      errors.shortDescription = "Informe a descrição curta.";
    }
    if (!form.description.trim()) {
      errors.description = "Informe a descrição completa.";
    }
    if (!editing && !imagePreview) {
      errors.image = "Selecione a foto principal do produto.";
      setImageError("Selecione a foto principal do produto.");
    }
    if (form.slug.trim() && !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(form.slug.trim())) {
      errors.slug = "Slug inválido (apenas a-z, 0-9 e hífens).";
    }
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function save() {
    if (!validateForm()) {
      push("error", "Formulário inválido. Revise os campos destacados.");
      return;
    }
    const price = Number(form.price.replace(",", "."));
    setSaving(true);
    try {
      const saved = await catalogUpsert(
        {
          id: editing?.id,
          slug: form.slug.trim() || slugify(form.name),
          createdAt: editing?.createdAt,
          rowVersion: editing?.rowVersion,
          name: form.name.trim(),
          shortDescription: form.shortDescription.trim(),
          description: form.description.trim(),
          price,
          category: form.category.trim(),
          categoryId: form.categoryId || undefined,
          images: [imagePreview as string],
          features: splitList(form.features),
          packageContents: splitList(form.packageContents),
          variations: parseVariations(form.variations, price),
          isFeatured: form.isFeatured,
          isAvailable: form.isAvailable,
          isDemo: false,
        },
        imageFile ?? undefined,
      );

      if (apiMode && saved.id) {
        const repo = getProductRepository();
        const detail = repo.getAdminDetail
          ? await repo.getAdminDetail(saved.id)
          : saved;
        if (detail) {
          setEditing(detail);
          setDetailImages(detail.productImages ?? []);
          setImageFile(null);
        }
      }

      push(
        "success",
        editing ? "Produto atualizado com sucesso." : "Produto cadastrado com sucesso.",
      );
      await load();
      if (!apiMode) setOpen(false);
    } catch (err) {
      push(
        "error",
        err instanceof Error ? err.message : "Falha ao salvar o produto.",
      );
    } finally {
      setSaving(false);
    }
  }

  async function toggleAvailability(product: Product) {
    setBusyId(product.id);
    try {
      await catalogSetAvailability(product.id, !product.isAvailable);
      push(
        "success",
        product.isAvailable
          ? "Produto marcado como indisponível."
          : "Produto marcado como disponível.",
      );
      await load();
    } catch (err) {
      push(
        "error",
        err instanceof Error ? err.message : "Falha ao atualizar disponibilidade.",
      );
    } finally {
      setBusyId(null);
    }
  }

  async function confirmArchive() {
    if (!archiveTarget) return;
    const repo = getProductRepository();
    setBusyId(archiveTarget.id);
    try {
      if (archiveTarget.isArchived) {
        if (!repo.restore) throw new Error("Restauração indisponível.");
        await repo.restore(archiveTarget.id);
        push("success", "Produto restaurado (permanece indisponível até você disponibilizar).");
      } else {
        if (!repo.archive) throw new Error("Arquivamento indisponível.");
        await repo.archive(archiveTarget.id);
        push("success", "Produto arquivado.");
      }
      setArchiveTarget(null);
      await load();
    } catch (err) {
      push("error", err instanceof Error ? err.message : "Falha na operação.");
    } finally {
      setBusyId(null);
    }
  }

  async function refreshDetailImages(productId: string) {
    const repo = getProductRepository();
    if (!repo.getAdminDetail) return;
    const detail = await repo.getAdminDetail(productId);
    if (detail) {
      setEditing(detail);
      setDetailImages(detail.productImages ?? []);
    }
  }

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-serif text-3xl text-esotera-secondary">Produtos</h1>
          <p className="mt-1 text-sm text-esotera-muted">
            {apiMode
              ? "Gestão real via API: cadastro, edição, imagens (Cloudinary) e arquivamento."
              : "Dados locais no navegador (modo mock). Upload com pré-visualização local."}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="secondary" onClick={() => void load()}>
            Atualizar lista
          </Button>
          <Button type="button" onClick={openCreate}>
            Novo produto
          </Button>
        </div>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <FormField label="Pesquisar" id="f-search">
          <input
            id="f-search"
            className={inputClassName}
            value={search}
            placeholder="Nome ou slug"
            onChange={(e) => setSearch(e.target.value)}
          />
        </FormField>
        <FormField label="Categoria" id="f-cat">
          <select
            id="f-cat"
            className={inputClassName}
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
          >
            <option value="">Todas</option>
            {apiMode
              ? categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))
              : Array.from(new Set(mockProducts.map((p) => p.category))).map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
          </select>
        </FormField>
        <FormField label="Disponibilidade" id="f-av">
          <select
            id="f-av"
            className={inputClassName}
            value={availabilityFilter}
            onChange={(e) =>
              setAvailabilityFilter(e.target.value as "all" | "yes" | "no")
            }
          >
            <option value="all">Todas</option>
            <option value="yes">Disponíveis</option>
            <option value="no">Indisponíveis</option>
          </select>
        </FormField>
        <FormField label="Arquivados" id="f-arch">
          <select
            id="f-arch"
            className={inputClassName}
            value={archivedFilter}
            onChange={(e) => setArchivedFilter(e.target.value as ArchiveFilter)}
          >
            <option value="active">Ativos</option>
            <option value="archived">Arquivados</option>
            <option value="all">Todos</option>
          </select>
        </FormField>
      </div>

      {loading ? (
        <p className="mt-6 text-sm text-esotera-muted" role="status">
          Carregando produtos…
        </p>
      ) : null}
      {error ? (
        <div className="mt-6 rounded-md border border-esotera-error/30 bg-esotera-error/5 p-4" role="alert">
          <p className="text-sm text-esotera-error">{error}</p>
          <Button type="button" className="mt-2" variant="secondary" onClick={() => void load()}>
            Tentar novamente
          </Button>
        </div>
      ) : null}
      {!loading && !error && displayList.length === 0 ? (
        <div className="mt-6 rounded-md border border-esotera-border bg-esotera-surface-secondary px-4 py-3 text-sm text-esotera-muted">
          <p>Nenhum produto encontrado no catálogo da API.</p>
          {apiMode ? (
            <p className="mt-1">
              Com o modo API, a lista vem do Neon (não do mock local). Use
              &quot;Novo produto&quot; após as categorias estarem disponíveis.
            </p>
          ) : null}
        </div>
      ) : null}

      <ul className="mt-6 space-y-3">
        {displayList.map((product) => {
          const mainImage = product.images.find((src) => Boolean(src?.trim()));
          return (
            <li
              key={product.id}
              className="rounded-lg border border-esotera-border bg-esotera-surface p-3 shadow-sm sm:p-4"
            >
              <div className="flex min-w-0 items-start gap-3 sm:items-center">
                <ProductThumbnail
                  src={mainImage}
                  alt={product.name}
                  sizeClassName="h-14 w-14 sm:h-16 sm:w-16"
                />
                <div className="min-w-0 flex-1">
                  <p className="line-clamp-2 font-medium leading-snug text-esotera-text sm:truncate">
                    {product.name}
                  </p>
                  <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-esotera-muted">
                    <span>{product.category}</span>
                    <span aria-hidden>·</span>
                    <Price value={product.price} className="text-sm" />
                    <span
                      className={
                        product.isAvailable
                          ? "rounded bg-esotera-success/10 px-1.5 py-0.5 text-esotera-success"
                          : "rounded bg-esotera-error/10 px-1.5 py-0.5 text-esotera-error"
                      }
                    >
                      {product.isAvailable ? "Disponível" : "Indisponível"}
                    </span>
                    {product.isFeatured ? (
                      <span className="rounded bg-esotera-primary/10 px-1.5 py-0.5 text-esotera-primary">
                        Destaque
                      </span>
                    ) : null}
                    {product.isArchived ? (
                      <span className="rounded bg-esotera-muted/20 px-1.5 py-0.5">
                        Arquivado
                      </span>
                    ) : null}
                  </div>
                </div>
              </div>
              <div className="mt-3 flex flex-col gap-2 border-t border-esotera-border pt-3 sm:flex-row sm:justify-end">
                <Button
                  type="button"
                  variant="secondary"
                  className="w-full sm:w-auto"
                  disabled={busyId === product.id}
                  onClick={() => void openEdit(product)}
                >
                  Editar
                </Button>
                <Button
                  type="button"
                  variant={product.isAvailable ? "ghost" : "primary"}
                  className="w-full sm:w-auto"
                  disabled={busyId === product.id || Boolean(product.isArchived)}
                  onClick={() => void toggleAvailability(product)}
                >
                  {product.isAvailable ? "Indisponibilizar" : "Disponibilizar"}
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  className="w-full sm:w-auto"
                  disabled={busyId === product.id}
                  onClick={() => setArchiveTarget(product)}
                >
                  {product.isArchived ? "Restaurar" : "Arquivar"}
                </Button>
              </div>
            </li>
          );
        })}
      </ul>

      {open ? (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-esotera-secondary/40 p-0 sm:items-center sm:p-4">
          <div
            role="dialog"
            aria-modal
            aria-labelledby="product-form-title"
            className="max-h-[92vh] w-full max-w-lg overflow-y-auto rounded-t-xl border border-esotera-border bg-esotera-surface p-5 shadow-xl sm:rounded-xl sm:p-6"
          >
            <h2 id="product-form-title" className="font-serif text-xl text-esotera-secondary">
              {editing ? "Editar produto" : "Novo produto"}
            </h2>

            <div className="mt-4 grid gap-3">
              <FormField label="Nome" id="p-name" required error={formErrors.name}>
                <input
                  id="p-name"
                  className={inputClassName}
                  value={form.name}
                  onChange={(e) => {
                    const name = e.target.value;
                    setForm((f) => ({
                      ...f,
                      name,
                      slug: editing ? f.slug : slugify(name),
                    }));
                  }}
                />
              </FormField>
              <FormField label="Slug" id="p-slug" error={formErrors.slug} hint="Não muda automaticamente ao editar o nome de um produto existente.">
                <input
                  id="p-slug"
                  className={inputClassName}
                  value={form.slug}
                  onChange={(e) => setForm({ ...form, slug: e.target.value })}
                />
              </FormField>
              <FormField label="Preço" id="p-price" required error={formErrors.price}>
                <input
                  id="p-price"
                  inputMode="decimal"
                  className={inputClassName}
                  value={form.price}
                  onChange={(e) => setForm({ ...form, price: e.target.value })}
                />
              </FormField>
              <FormField label="Categoria" id="p-category" required error={formErrors.category}>
                {apiMode ? (
                  <>
                    <select
                      id="p-category"
                      name="categoryId"
                      className={inputClassName}
                      value={form.categoryId}
                      onChange={(e) => {
                        const cat = categories.find((c) => c.id === e.target.value);
                        setForm({
                          ...form,
                          categoryId: e.target.value,
                          category: cat?.name ?? "",
                        });
                      }}
                      disabled={categories.length === 0}
                    >
                      <option value="">
                        {categories.length === 0
                          ? "Nenhuma categoria disponível…"
                          : "Selecione…"}
                      </option>
                      {categories.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}
                        </option>
                      ))}
                    </select>
                    {categories.length === 0 ? (
                      <p className="mt-1 text-xs text-esotera-error" role="alert">
                        Nenhuma categoria no banco. Após o deploy da API com
                        bootstrap de catálogo, recarregue esta página. Sem
                        categoria não é possível cadastrar produtos.
                      </p>
                    ) : null}
                  </>
                ) : (
                  <input
                    id="p-category"
                    name="category"
                    className={inputClassName}
                    value={form.category}
                    onChange={(e) => setForm({ ...form, category: e.target.value })}
                  />
                )}
              </FormField>
              <FormField label="Descrição curta" id="p-short" required error={formErrors.shortDescription}>
                <input
                  id="p-short"
                  className={inputClassName}
                  value={form.shortDescription}
                  onChange={(e) => setForm({ ...form, shortDescription: e.target.value })}
                />
              </FormField>
              <FormField label="Descrição completa" id="p-desc" required error={formErrors.description}>
                <textarea
                  id="p-desc"
                  className={inputClassName}
                  rows={4}
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                />
              </FormField>

              {!editing || !apiMode ? (
                <div className="space-y-2">
                  <p className="text-sm font-medium text-esotera-secondary">
                    Foto principal {!editing ? <span className="text-esotera-primary">*</span> : null}
                  </p>
                  <p className="text-xs text-esotera-muted">
                    PNG, JPG ou WebP · máximo {(MAX_IMAGE_BYTES / (1024 * 1024)).toFixed(0)} MB.
                  </p>
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    className="block w-full text-sm text-esotera-muted file:mr-3 file:rounded-md file:border-0 file:bg-esotera-primary file:px-3 file:py-2 file:text-sm file:font-medium file:text-white"
                    onChange={(e) => void onFileChange(e.target.files?.[0] ?? null)}
                  />
                  {imageError || formErrors.image ? (
                    <p role="alert" className="text-xs text-esotera-error">
                      {imageError || formErrors.image}
                    </p>
                  ) : null}
                  {imagePreview ? (
                    <div className="relative mt-2 aspect-square w-40 overflow-hidden rounded-lg border border-esotera-border">
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img src={imagePreview} alt="Pré-visualização" className="h-full w-full object-cover" />
                    </div>
                  ) : null}
                </div>
              ) : null}

              <FormField label="Características" id="p-feat" hint="Separe por vírgula">
                <input
                  id="p-feat"
                  className={inputClassName}
                  value={form.features}
                  onChange={(e) => setForm({ ...form, features: e.target.value })}
                />
              </FormField>
              <FormField label="Conteúdo da embalagem" id="p-pack" hint="Separe por vírgula">
                <input
                  id="p-pack"
                  className={inputClassName}
                  value={form.packageContents}
                  onChange={(e) => setForm({ ...form, packageContents: e.target.value })}
                />
              </FormField>
              <FormField
                label="Variações"
                id="p-var"
                hint="Uma por linha: Nome|preço|1 (disponível) ou 0 (indisponível). Ex.: Somente Tarô|54.90|1"
              >
                <textarea
                  id="p-var"
                  className={inputClassName}
                  rows={4}
                  value={form.variations}
                  onChange={(e) => setForm({ ...form, variations: e.target.value })}
                />
              </FormField>

              <label className="flex min-h-11 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={form.isAvailable}
                  onChange={(e) => setForm({ ...form, isAvailable: e.target.checked })}
                />
                Disponível
              </label>
              <label className="flex min-h-11 items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={form.isFeatured}
                  onChange={(e) => setForm({ ...form, isFeatured: e.target.checked })}
                />
                Produto em destaque
              </label>

              {apiMode && editing ? (
                <ProductImageManager
                  productId={editing.id}
                  images={detailImages}
                  busy={saving}
                  onUpload={async (file, isPrimary) => {
                    const repo = getProductRepository();
                    if (!repo.uploadImage) return;
                    await repo.uploadImage(editing.id, file, { isPrimary });
                    await refreshDetailImages(editing.id);
                    await load();
                    push("success", "Imagem enviada.");
                  }}
                  onSetPrimary={async (imageId) => {
                    const repo = getProductRepository();
                    if (!repo.updateImage) return;
                    await repo.updateImage(editing.id, imageId, { isPrimary: true });
                    await refreshDetailImages(editing.id);
                    push("success", "Imagem principal atualizada.");
                  }}
                  onUpdateAlt={async (imageId, altText) => {
                    const repo = getProductRepository();
                    if (!repo.updateImage) return;
                    await repo.updateImage(editing.id, imageId, { altText });
                    await refreshDetailImages(editing.id);
                    push("success", "Texto alternativo salvo.");
                  }}
                  onDelete={async (imageId) => {
                    const repo = getProductRepository();
                    if (!repo.deleteImage) return;
                    await repo.deleteImage(editing.id, imageId);
                    await refreshDetailImages(editing.id);
                    await load();
                    push("success", "Imagem removida.");
                  }}
                  onReorder={async (imageIds) => {
                    const repo = getProductRepository();
                    if (!repo.reorderImages) return;
                    const next = await repo.reorderImages(editing.id, imageIds);
                    setDetailImages(next);
                    await load();
                  }}
                />
              ) : null}
            </div>

            <div className="mt-6 flex flex-wrap justify-end gap-2">
              <Button type="button" variant="secondary" onClick={() => setOpen(false)} disabled={saving}>
                Fechar
              </Button>
              <Button type="button" onClick={() => void save()} disabled={saving}>
                {saving ? "Salvando…" : "Salvar"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmModal
        open={Boolean(archiveTarget)}
        title={archiveTarget?.isArchived ? "Restaurar produto" : "Arquivar produto"}
        description={
          archiveTarget?.isArchived
            ? "O produto voltará ao admin como ativo, mas permanecerá indisponível para compra até você disponibilizá-lo."
            : "O produto sairá do catálogo e não poderá ser comprado. Pedidos antigos permanecem intactos."
        }
        confirmLabel={archiveTarget?.isArchived ? "Restaurar" : "Arquivar"}
        busy={Boolean(busyId)}
        onCancel={() => setArchiveTarget(null)}
        onConfirm={() => void confirmArchive()}
      />
    </div>
  );
}
