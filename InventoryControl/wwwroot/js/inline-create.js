// Inline creation of categories and suppliers from product forms
(function () {
    'use strict';

    function getAntiForgeryToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    // Category inline creation
    const btnSaveCategory = document.getElementById('btnSaveCategory');
    if (btnSaveCategory) {
        btnSaveCategory.addEventListener('click', async function () {
            const name = document.getElementById('newCategoryName').value.trim();
            const description = document.getElementById('newCategoryDescription').value.trim();
            const errorsDiv = document.getElementById('categoryModalErrors');

            if (!name) {
                errorsDiv.textContent = 'O nome é obrigatório.';
                errorsDiv.classList.remove('d-none');
                return;
            }

            btnSaveCategory.disabled = true;
            try {
                const res = await fetch('/Categories/CreateInline', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    body: JSON.stringify({ name, description: description || null })
                });

                if (res.ok) {
                    const data = await res.json();
                    const select = document.getElementById('CategoryId');
                    if (select) {
                        const option = new Option(data.fullName || data.name, data.id, true, true);
                        select.add(option);
                    }
                    bootstrap.Modal.getInstance(document.getElementById('categoryModal')).hide();
                    document.getElementById('newCategoryName').value = '';
                    document.getElementById('newCategoryDescription').value = '';
                    errorsDiv.classList.add('d-none');
                } else {
                    const data = await res.json();
                    errorsDiv.textContent = data.errors ? data.errors.join(', ') : 'Erro ao criar categoria.';
                    errorsDiv.classList.remove('d-none');
                }
            } catch (e) {
                errorsDiv.textContent = 'Falha na requisição: ' + e.message;
                errorsDiv.classList.remove('d-none');
            } finally {
                btnSaveCategory.disabled = false;
            }
        });
    }

    // Supplier inline creation
    const btnSaveSupplier = document.getElementById('btnSaveSupplier');
    if (btnSaveSupplier) {
        btnSaveSupplier.addEventListener('click', async function () {
            const name = document.getElementById('newSupplierName').value.trim();
            const contactName = document.getElementById('newSupplierContactName').value.trim();
            const phone = document.getElementById('newSupplierPhone').value.trim();
            const email = document.getElementById('newSupplierEmail').value.trim();
            const errorsDiv = document.getElementById('supplierModalErrors');

            if (!name) {
                errorsDiv.textContent = 'O nome é obrigatório.';
                errorsDiv.classList.remove('d-none');
                return;
            }

            btnSaveSupplier.disabled = true;
            try {
                const res = await fetch('/Suppliers/CreateInline', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    body: JSON.stringify({
                        name,
                        contactName: contactName || null,
                        phone: phone || null,
                        email: email || null
                    })
                });

                if (res.ok) {
                    const data = await res.json();
                    const select = document.getElementById('SupplierId');
                    if (select) {
                        const option = new Option(data.name, data.id, true, true);
                        select.add(option);
                    }
                    bootstrap.Modal.getInstance(document.getElementById('supplierModal')).hide();
                    document.getElementById('newSupplierName').value = '';
                    document.getElementById('newSupplierContactName').value = '';
                    document.getElementById('newSupplierPhone').value = '';
                    document.getElementById('newSupplierEmail').value = '';
                    errorsDiv.classList.add('d-none');
                } else {
                    const data = await res.json();
                    errorsDiv.textContent = data.errors ? data.errors.join(', ') : 'Erro ao criar fornecedor.';
                    errorsDiv.classList.remove('d-none');
                }
            } catch (e) {
                errorsDiv.textContent = 'Falha na requisição: ' + e.message;
                errorsDiv.classList.remove('d-none');
            } finally {
                btnSaveSupplier.disabled = false;
            }
        });
    }
})();
