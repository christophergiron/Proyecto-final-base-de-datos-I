(function () {
    'use strict';

    var campo = document.getElementById('filtroReportes');
    var lista = document.getElementById('listaReportes');
    if (!campo || !lista) return;

    var aviso = document.getElementById('sinReportes');
    var items = Array.prototype.slice.call(lista.querySelectorAll('[data-reporte]'));

    function normalizar(texto) {
        return (texto || '').toString().normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    }

    campo.addEventListener('input', function () {
        var termino = normalizar(campo.value).trim();
        var visibles = 0;

        items.forEach(function (item) {
            var coincide = termino === '' || normalizar(item.getAttribute('data-texto')).indexOf(termino) !== -1;
            item.classList.toggle('d-none', !coincide);
            if (coincide) visibles++;
        });

        if (aviso) aviso.classList.toggle('d-none', visibles !== 0);
    });
})();
