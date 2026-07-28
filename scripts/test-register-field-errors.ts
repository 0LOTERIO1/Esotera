/**
 * Testes unitários leves do mapeamento de erros do cadastro.
 * Executar: npx --yes tsx scripts/test-register-field-errors.ts
 */
import assert from "node:assert/strict";
import { ApiError } from "../src/services/api/apiClient";
import {
  inferRegisterErrorField,
  mapRegisterApiFieldErrors,
} from "../src/utils/registerFieldErrors";
import { onlyDigits, validateCpf, validateEmail } from "../src/utils/validation";

function testMapCpfErrorStaysOnCpf() {
  const error = new ApiError(400, "Erro de validação", "Um ou mais erros", {
    Cpf: ["CPF deve conter 11 dígitos"],
  });
  const mapped = mapRegisterApiFieldErrors(error);
  assert.equal(mapped.cpf, "CPF deve conter 11 dígitos");
  assert.equal(mapped.email, undefined);
}

function testMapEmailErrorStaysOnEmail() {
  const error = new ApiError(400, "Erro de validação", undefined, {
    email: ["Email inválido."],
  });
  const mapped = mapRegisterApiFieldErrors(error);
  assert.equal(mapped.email, "Email inválido.");
  assert.equal(mapped.cpf, undefined);
}

function testInferFromMessage() {
  assert.equal(inferRegisterErrorField("CPF deve conter 11 dígitos"), "cpf");
  assert.equal(inferRegisterErrorField("E-mail inválido."), "email");
}

function testCpfNormalizationAndValidation() {
  assert.equal(onlyDigits("529.982.247-25"), "52998224725");
  assert.equal(validateCpf("529.982.247-25"), true);
  assert.equal(validateCpf("123.456"), false);
  assert.equal(onlyDigits("123.456").length, 6);
}

function testInvalidEmail() {
  assert.equal(validateEmail("nao-e-email"), false);
  assert.equal(validateEmail("ok@example.com"), true);
}

testMapCpfErrorStaysOnCpf();
testMapEmailErrorStaysOnEmail();
testInferFromMessage();
testCpfNormalizationAndValidation();
testInvalidEmail();

console.log("register field error tests: OK");
