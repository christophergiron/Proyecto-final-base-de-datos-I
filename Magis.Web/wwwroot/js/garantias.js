(function () {
    'use strict';

    var marca = document.getElementById('Form_CodigoMarca');
    var linea = document.getElementById('Form_CodigoLinea');
    if (!marca || !linea) return;

    function escapar(texto) {
        return String(texto).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    // Llena la línea según la marca 
    function cargarLineas(codigoMarca) {
        if (!codigoMarca) {
            linea.innerHTML = '<option value="">— Seleccione una marca —</option>';
            return;
        }

        linea.innerHTML = '<option value="">Cargando...</option>';
        linea.disabled = true;

        fetch(linea.getAttribute('data-url') + '?marca=' + encodeURIComponent(codigoMarca),
              { headers: { 'Accept': 'application/json' } })
            .then(function (respuesta) {
                if (!respuesta.ok) throw new Error('No se pudo cargar');
                return respuesta.json();
            })
            .then(function (lista) {
                var html = '<option value="">— Seleccione —</option>';
                lista.forEach(function (o) {
                    html += '<option value="' + escapar(o.valor) + '">' + escapar(o.texto) + '</option>';
                });
                linea.innerHTML = html;
            })
            .catch(function () {
                linea.innerHTML = '<option value="">No se pudieron cargar las líneas</option>';
            })
            .then(function () { linea.disabled = false; });
    }

    marca.addEventListener('change', function () { cargarLineas(marca.value); });
})();
