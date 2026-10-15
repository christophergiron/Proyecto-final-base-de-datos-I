(function () {
    'use strict';

    var formulario = document.getElementById('formCredito');
    if (!formulario) return;

    function normalizar(texto) {
        return (texto || '').toString().normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    }
    function valor(id) {
        var campo = document.getElementById(id);
        return campo ? campo.value.trim() : '';
    }

    // Buscadores 

    function configurarSelector(idTexto, idCodigo, idOculto, alElegir) {
        var texto = document.getElementById(idTexto);
        var codigo = document.getElementById(idCodigo);
        var oculto = document.getElementById(idOculto);
        if (!texto || !codigo) return;

        var lista = document.getElementById(texto.getAttribute('list'));

        function buscarOpcion() {
            if (!lista) return null;
            var opciones = lista.querySelectorAll('option');
            for (var i = 0; i < opciones.length; i++) {
                if (opciones[i].value === texto.value) return opciones[i];
            }
            return null;
        }

        function sincronizar() {
            var opcion = buscarOpcion();
            if (opcion) {
                codigo.value = opcion.getAttribute('data-codigo') || '';
                texto.setCustomValidity('');
                if (alElegir) alElegir(opcion);
            } else {
                codigo.value = '';
                texto.setCustomValidity(texto.value === '' ? 'Elija una opción de la lista.' : 'Elija una opción de la lista.');
            }
            if (oculto) oculto.value = texto.value;
        }

        texto.addEventListener('input', sincronizar);
        texto.addEventListener('change', sincronizar);
        // Al volver a mostrar el formulario con datos
        if (codigo.value === '') {
            sincronizar();
        } else {
            texto.setCustomValidity('');
        }
    }

    var simValor = document.getElementById('simValorAgencia');
    var ayudaValor = document.getElementById('ayudaValorAgencia');
    var valorAutomatico = true;

    if (simValor) {
        simValor.addEventListener('input', function () { valorAutomatico = simValor.value === ''; });
    }

    configurarSelector('clienteTexto', 'CodigoDeudor', 'clienteTextoOculto');
    configurarSelector('garantiaTexto', 'CodigoGarantia', 'garantiaTextoOculto', function (opcion) {
        var v = opcion.getAttribute('data-valor');
        if (!simValor) return;
        if (v && (simValor.value === '' || valorAutomatico)) {
            simValor.value = v;
            valorAutomatico = true;
            if (ayudaValor) ayudaValor.textContent = 'Valor de agencia registrado para este vehículo.';
        } else if (!v && ayudaValor && simValor.value === '') {
            ayudaValor.textContent = 'Este vehículo no tiene valor registrado: escríbalo para simular.';
        }
    });

    // Simulador
    var resultadoSim = document.getElementById('resultadoSimulacion');
    var resultadoPlan = document.getElementById('resultadoPlan');
    var panelPlan = document.getElementById('panelPlan');
    var botonSimular = document.getElementById('botonSimular');
    var botonPlan = document.getElementById('botonPlan');

    function parametros(conDepreciacion) {
        var p = new URLSearchParams();
        p.set('valorAgencia', valor('simValorAgencia'));
        p.set('enganche', valor('Enganche'));
        p.set('plazo', valor('Plazo'));
        p.set('tasaInteres', valor('TasaInteres'));
        if (conDepreciacion) p.set('porcentajeDepreciacion', valor('simDepreciacion'));
        return p.toString();
    }

    function pedir(url, destino, boton, alTerminar) {
        var textoBoton = boton.innerHTML;
        boton.disabled = true;
        boton.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Calculando...';

        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) {
                if (!r.ok) throw new Error('Error ' + r.status);
                return r.text();
            })
            .then(function (html) {
                destino.innerHTML = html;
                if (alTerminar) alTerminar();
            })
            .catch(function () {
                destino.innerHTML = '<div class="alert alert-danger mb-0">No se pudo calcular. Intente de nuevo.</div>';
                if (alTerminar) alTerminar();
            })
            .finally(function () {
                boton.disabled = false;
                boton.innerHTML = textoBoton;
            });
    }

    function simular() {
        if (!botonSimular || !resultadoSim) return;
        pedir(formulario.getAttribute('data-url-simular') + '?' + parametros(true), resultadoSim, botonSimular);
    }

    function verPlan() {
        if (!botonPlan || !resultadoPlan) return;
        pedir(formulario.getAttribute('data-url-plan') + '?' + parametros(false), resultadoPlan, botonPlan, function () {
            if (panelPlan) {
                panelPlan.classList.remove('d-none');
                panelPlan.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });
    }

    if (botonSimular) botonSimular.addEventListener('click', simular);
    if (botonPlan) botonPlan.addEventListener('click', verPlan);

    // Enter dentro de los campos del simulador simula
    ['simValorAgencia', 'simDepreciacion', 'Enganche', 'Plazo', 'TasaInteres'].forEach(function (id) {
        var campo = document.getElementById(id);
        if (!campo) return;
        campo.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                simular();
            }
        });
    });

    // Referencias: filtro en vivo y contador 
    var contador = document.getElementById('contadorReferencias');
    var filtro = document.getElementById('filtroReferencias');
    var lista = document.getElementById('listaReferencias');
    var minimo = contador ? parseInt(contador.getAttribute('data-minimo') || '3', 10) : 3;

    function marcadas() {
        return lista ? lista.querySelectorAll('input[type="checkbox"]:checked').length : 0;
    }

    function actualizarContador() {
        if (!contador) return;
        var n = marcadas();
        contador.textContent = n + (n === 1 ? ' seleccionada' : ' seleccionadas') + ' · mínimo ' + minimo;
        contador.classList.toggle('mg-contador-ok', n >= minimo);
    }

    if (lista) {
        lista.addEventListener('change', actualizarContador);
        actualizarContador();
    }

    if (filtro && lista) {
        filtro.addEventListener('input', function () {
            var termino = normalizar(filtro.value).trim();
            lista.querySelectorAll('.mg-ref').forEach(function (item) {
                var coincide = termino === '' || normalizar(item.getAttribute('data-texto')).indexOf(termino) !== -1;
                item.style.display = coincide ? '' : 'none';
            });
        });
        filtro.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') e.preventDefault();
        });
    }

    // Fiadores nuevos: agregar / quitar
    var contenedor = document.getElementById('fiadoresNuevos');
    var molde = document.getElementById('moldeFiador');
    var botonAgregar = document.getElementById('agregarFiador');
    var siguiente = contenedor ? contenedor.querySelectorAll('[data-fiador]').length : 0;

    if (botonAgregar && molde && contenedor) {
        botonAgregar.addEventListener('click', function () {
            var html = molde.innerHTML.replace(/__i__/g, String(siguiente++));
            var envoltorio = document.createElement('div');
            envoltorio.innerHTML = html;
            var bloque = envoltorio.firstElementChild;
            while (bloque && !bloque.hasAttribute('data-fiador')) bloque = bloque.nextElementSibling;
            if (!bloque) return;
            contenedor.appendChild(bloque);
            var primero = bloque.querySelector('input');
            if (primero) primero.focus();
        });

        contenedor.addEventListener('click', function (e) {
            var quitar = e.target.closest ? e.target.closest('[data-quitar-fiador]') : null;
            if (!quitar) return;
            var bloque = quitar.closest('[data-fiador]');
            if (bloque) bloque.remove();
        });
    }

    // Deja los índices contiguos para que el servidor reciba la lista completa
    function reindexarFiadores() {
        if (!contenedor) return;
        contenedor.querySelectorAll('[data-fiador]').forEach(function (bloque, indice) {
            bloque.querySelectorAll('[name]').forEach(function (campo) {
                campo.name = campo.name.replace(/Form\.NuevosFiadores\[\d+\]/, 'Form.NuevosFiadores[' + indice + ']');
            });
        });
    }

    // Validación al emitir
    var aviso = document.createElement('div');
    aviso.className = 'alert alert-warning d-none';
    aviso.setAttribute('role', 'alert');
    var seccionRef = lista ? lista.closest('section') : null;
    if (seccionRef) seccionRef.insertBefore(aviso, lista.previousElementSibling || lista);

    formulario.addEventListener('submit', function (evento) {
        reindexarFiadores();
        if (marcadas() < minimo) {
            evento.preventDefault();
            aviso.textContent = 'Seleccione al menos ' + minimo + ' referencias para emitir el crédito.';
            aviso.classList.remove('d-none');
            if (seccionRef) seccionRef.scrollIntoView({ behavior: 'smooth', block: 'center' });
        } else {
            aviso.classList.add('d-none');
        }
    });

    // Que los mensajes nativos también cancelen el bloqueo de doble envío
    formulario.addEventListener('invalid', function () {
        formulario.removeAttribute('data-enviado');
    }, true);
})();
