(function () {
    'use strict';

    var modalTelefono = document.getElementById('modalTelefono');
    var modalDireccion = document.getElementById('modalDireccion');
    if (!modalTelefono || !modalDireccion || !window.bootstrap) return;

    function campo(id) { return document.getElementById(id); }

    function escapar(texto) {
        return String(texto).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function mostrar(modal) {
        window.bootstrap.Modal.getOrCreateInstance(modal).show();
    }

    // Al cerrar una ventana se quita el bloqueo de procesando
    function reiniciarEnvio(modal) {
        modal.addEventListener('hidden.bs.modal', function () {
            var formulario = modal.querySelector('form');
            if (!formulario) return;
            formulario.removeAttribute('data-enviado');
            formulario.querySelectorAll('button[type="submit"]').forEach(function (boton) {
                var original = boton.getAttribute('data-texto-original');
                if (original !== null) boton.innerHTML = original;
                boton.disabled = false;
            });
        });
    }
    reiniciarEnvio(modalTelefono);
    reiniciarEnvio(modalDireccion);

    //Teléfonos

    function abrirTelefono(datos) {
        campo('tituloTelefono').textContent = datos ? 'Editar teléfono' : 'Agregar teléfono';
        campo('telCorrelativo').value = datos ? datos.correlativo || '' : '';
        campo('telNumero').value = datos ? datos.numero || '' : '';
        campo('telTipo').value = datos ? datos.tipo || '' : '';
        mostrar(modalTelefono);
    }

    document.querySelectorAll('[data-nuevo-telefono]').forEach(function (boton) {
        boton.addEventListener('click', function () { abrirTelefono(null); });
    });

    document.querySelectorAll('[data-editar-telefono]').forEach(function (boton) {
        boton.addEventListener('click', function () {
            abrirTelefono({
                correlativo: boton.getAttribute('data-correlativo'),
                numero: boton.getAttribute('data-numero'),
                tipo: boton.getAttribute('data-tipo')
            });
        });
    });

    modalTelefono.addEventListener('shown.bs.modal', function () { campo('telNumero').focus(); });

    // ---------------------------------------------------------------- Direcciones

    var departamento = campo('dirDepartamento');
    var municipio = campo('dirMunicipio');

    // Llena el municipio según el departamento 
    function cargarMunicipios(codigoDepartamento, seleccionado) {
        if (!codigoDepartamento) {
            municipio.innerHTML = '<option value="">— Seleccione un departamento —</option>';
            return Promise.resolve();
        }

        municipio.innerHTML = '<option value="">Cargando...</option>';
        municipio.disabled = true;

        return fetch(municipio.getAttribute('data-url') + '?departamento=' + encodeURIComponent(codigoDepartamento),
                     { headers: { 'Accept': 'application/json' } })
            .then(function (respuesta) {
                if (!respuesta.ok) throw new Error('No se pudo cargar');
                return respuesta.json();
            })
            .then(function (lista) {
                var html = '<option value="">— Seleccione —</option>';
                lista.forEach(function (o) {
                    var elegido = seleccionado && String(o.valor) === String(seleccionado) ? ' selected' : '';
                    html += '<option value="' + escapar(o.valor) + '"' + elegido + '>' + escapar(o.texto) + '</option>';
                });
                municipio.innerHTML = html;
            })
            .catch(function () {
                municipio.innerHTML = '<option value="">No se pudieron cargar los municipios</option>';
            })
            .then(function () { municipio.disabled = false; });
    }

    departamento.addEventListener('change', function () { cargarMunicipios(departamento.value, null); });

    function abrirDireccion(datos) {
        campo('tituloDireccion').textContent = datos ? 'Editar dirección' : 'Agregar dirección';
        campo('dirCorrelativo').value = datos ? datos.correlativo || '' : '';
        campo('dirZona').value = datos ? datos.zona || '' : '';
        campo('dirCalle').value = datos ? datos.calle || '' : '';
        campo('dirAvenida').value = datos ? datos.avenida || '' : '';
        campo('dirTipo').value = datos ? datos.tipo || '' : '';
        departamento.value = datos ? datos.departamento || '' : '';
        cargarMunicipios(departamento.value, datos ? datos.municipio : null);
        mostrar(modalDireccion);
    }

    document.querySelectorAll('[data-nueva-direccion]').forEach(function (boton) {
        boton.addEventListener('click', function () { abrirDireccion(null); });
    });

    document.querySelectorAll('[data-editar-direccion]').forEach(function (boton) {
        boton.addEventListener('click', function () {
            abrirDireccion({
                correlativo: boton.getAttribute('data-correlativo'),
                zona: boton.getAttribute('data-zona'),
                calle: boton.getAttribute('data-calle'),
                avenida: boton.getAttribute('data-avenida'),
                departamento: boton.getAttribute('data-departamento'),
                municipio: boton.getAttribute('data-municipio'),
                tipo: boton.getAttribute('data-tipo')
            });
        });
    });
})();
